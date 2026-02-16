using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Domain.Entities;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;
using Helpdesk.Light.Infrastructure.Identity;
using Helpdesk.Light.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Light.UnitTests;

public sealed class AnalyticsServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_ComputesExpectedRangeAndFirstResponseMetrics()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using HelpdeskDbContext dbContext = CreateDbContext(connection);

        Guid customerId = SeedDataConstants.ContosoCustomerId;
        Guid endUserId = Guid.NewGuid();

        dbContext.Customers.Add(new Customer(customerId, "Contoso"));
        dbContext.Users.Add(new ApplicationUser
        {
            Id = endUserId,
            UserName = "enduser@contoso.com",
            NormalizedUserName = "ENDUSER@CONTOSO.COM",
            Email = "enduser@contoso.com",
            NormalizedEmail = "ENDUSER@CONTOSO.COM",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            DisplayName = "Contoso End User",
            CustomerId = customerId
        });

        DateTime ticketOneCreatedUtc = new(2026, 02, 10, 10, 00, 00, DateTimeKind.Utc);
        DateTime ticketTwoCreatedUtc = ticketOneCreatedUtc.AddMinutes(30);

        Ticket ticketOne = new(
            Guid.NewGuid(),
            customerId,
            endUserId,
            TicketChannel.Email,
            "Mail outage",
            "Inbound mail is delayed.",
            TicketPriority.High,
            ticketOneCreatedUtc);

        Ticket ticketTwo = new(
            Guid.NewGuid(),
            customerId,
            endUserId,
            TicketChannel.Web,
            "Portal issue",
            "Ticket portal page fails to load.",
            TicketPriority.Low,
            ticketTwoCreatedUtc);

        ticketTwo.TransitionStatus(TicketStatus.Resolved, ticketTwoCreatedUtc.AddMinutes(45));

        dbContext.Tickets.AddRange(ticketOne, ticketTwo);

        dbContext.TicketMessages.AddRange(
            new TicketMessage(
                Guid.NewGuid(),
                ticketOne.Id,
                TicketAuthorType.EndUser,
                endUserId,
                "Initial report",
                TicketMessageSource.Web,
                null,
                ticketOneCreatedUtc.AddMinutes(2)),
            new TicketMessage(
                Guid.NewGuid(),
                ticketOne.Id,
                TicketAuthorType.Technician,
                Guid.NewGuid(),
                "Acknowledged and investigating",
                TicketMessageSource.Web,
                null,
                ticketOneCreatedUtc.AddMinutes(8)),
            new TicketMessage(
                Guid.NewGuid(),
                ticketTwo.Id,
                TicketAuthorType.Agent,
                null,
                "Automated first response",
                TicketMessageSource.Ai,
                null,
                ticketTwoCreatedUtc.AddMinutes(10)));

        await dbContext.SaveChangesAsync();

        TestTenantContextAccessor tenantContext = new(new TenantAccessContext(
            Guid.NewGuid(),
            "admin@msp.local",
            RoleNames.MspAdmin,
            null));

        AnalyticsService service = new(dbContext, tenantContext);

        AnalyticsDashboardDto dashboard = await service.GetDashboardAsync(new AnalyticsDashboardRequest(
            customerId,
            ticketOneCreatedUtc.AddHours(-1),
            ticketTwoCreatedUtc.AddHours(2)));

        Assert.Equal(2, dashboard.TotalTicketVolume);
        Assert.Equal(1, dashboard.OpenTicketCount);
        Assert.Equal(1, dashboard.OpenTicketsByPriority[TicketPriority.High.ToString()]);
        Assert.Equal(0, dashboard.OpenTicketsByPriority[TicketPriority.Low.ToString()]);
        Assert.Equal(1, dashboard.ChannelSplit[TicketChannel.Email.ToString()]);
        Assert.Equal(1, dashboard.ChannelSplit[TicketChannel.Web.ToString()]);
        Assert.NotNull(dashboard.AverageFirstResponseMinutes);
        Assert.Equal(9d, dashboard.AverageFirstResponseMinutes!.Value, 3);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static HelpdeskDbContext CreateDbContext(SqliteConnection connection)
    {
        DbContextOptions<HelpdeskDbContext> options = new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseSqlite(connection)
            .Options;

        HelpdeskDbContext dbContext = new(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private sealed class TestTenantContextAccessor(TenantAccessContext context) : ITenantContextAccessor
    {
        public TenantAccessContext Current => context;
    }
}
