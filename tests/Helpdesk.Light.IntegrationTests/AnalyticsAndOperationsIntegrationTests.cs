using System.Net;
using System.Net.Http.Json;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Contracts.Tickets;
using Helpdesk.Light.Domain.Email;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Light.IntegrationTests;

public sealed class AnalyticsAndOperationsIntegrationTests(HelpdeskApiFactory factory) : IClassFixture<HelpdeskApiFactory>
{
    [Fact]
    public async Task Dashboard_IsFilterableAndTenantScoped()
    {
        using HttpClient contosoUserClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(contosoUserClient, SeedDataConstants.ContosoEndUserEmail);

        await contosoUserClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            null,
            "Contoso dashboard validation",
            "Create analytics event in contoso tenant.",
            TicketPriority.Medium));

        using HttpClient fabrikamUserClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(fabrikamUserClient, SeedDataConstants.FabrikamEndUserEmail);

        await fabrikamUserClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            null,
            "Fabrikam dashboard validation",
            "Create analytics event in fabrikam tenant.",
            TicketPriority.High));

        string fromUtc = Uri.EscapeDataString(DateTime.UtcNow.AddHours(-2).ToString("O"));
        string toUtc = Uri.EscapeDataString(DateTime.UtcNow.AddHours(1).ToString("O"));

        using HttpClient contosoTechClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(contosoTechClient, SeedDataConstants.ContosoTechEmail);

        HttpResponseMessage forbidden = await contosoTechClient.GetAsync(
            $"/api/v1/analytics/dashboard?customerId={SeedDataConstants.FabrikamCustomerId}&fromUtc={fromUtc}&toUtc={toUtc}");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using HttpClient adminClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(adminClient, SeedDataConstants.AdminEmail);

        HttpResponseMessage contosoDashboardResponse = await adminClient.GetAsync(
            $"/api/v1/analytics/dashboard?customerId={SeedDataConstants.ContosoCustomerId}&fromUtc={fromUtc}&toUtc={toUtc}");

        contosoDashboardResponse.EnsureSuccessStatusCode();
        Assert.True(contosoDashboardResponse.Headers.Contains("X-Correlation-ID"));

        AnalyticsDashboardDto contosoDashboard = (await contosoDashboardResponse.Content.ReadFromJsonAsync<AnalyticsDashboardDto>(TestAuth.JsonOptions))!;
        Assert.True(contosoDashboard.TotalTicketVolume >= 1);
        Assert.True(contosoDashboard.ChannelSplit.GetValueOrDefault("Web") >= 1);
    }

    [Fact]
    public async Task HealthEndpoints_AreAnonymous()
    {
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage live = await client.GetAsync("/health/live");
        HttpResponseMessage ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        string liveBody = await live.Content.ReadAsStringAsync();
        string readyBody = await ready.Content.ReadAsStringAsync();

        Assert.Contains("status", liveBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("status", readyBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_CanQueryAndRetryDeadLetterMessages()
    {
        Guid deadLetterId;

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            HelpdeskDbContext dbContext = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
            OutboundEmailMessage message = new(
                Guid.NewGuid(),
                null,
                SeedDataConstants.ContosoCustomerId,
                SeedDataConstants.ContosoEndUserEmail,
                "Dead letter test",
                "This should be retried",
                $"dead-letter-test:{Guid.NewGuid():N}",
                DateTime.UtcNow);

            message.MarkAttempt();
            message.MarkAttempt();
            message.MarkAttempt();
            message.MarkDeadLetter("Forced dead letter for integration test", DateTime.UtcNow);
            dbContext.OutboundEmailMessages.Add(message);
            await dbContext.SaveChangesAsync();
            deadLetterId = message.Id;
        }

        using HttpClient adminClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(adminClient, SeedDataConstants.AdminEmail);

        HttpResponseMessage deadLetterListResponse = await adminClient.GetAsync("/api/v1/ops/dead-letters?take=100");
        deadLetterListResponse.EnsureSuccessStatusCode();

        List<DeadLetterMessageDto> deadLetters = (await deadLetterListResponse.Content.ReadFromJsonAsync<List<DeadLetterMessageDto>>(TestAuth.JsonOptions))!;
        Assert.Contains(deadLetters, item => item.Id == deadLetterId);

        HttpResponseMessage retryResponse = await adminClient.PostAsync("/api/v1/email/outbound/retry-dead-letter?take=100", null);
        retryResponse.EnsureSuccessStatusCode();

        using IServiceScope verifyScope = factory.Services.CreateScope();
        HelpdeskDbContext verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
        OutboundEmailMessage? retried = await verifyDbContext.OutboundEmailMessages.SingleOrDefaultAsync(item => item.Id == deadLetterId);

        Assert.NotNull(retried);
        Assert.Equal(OutboundEmailStatus.Sent, retried!.Status);
    }

    [Fact]
    public async Task AdminActions_WriteAuditTrailEvents()
    {
        using HttpClient adminClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(adminClient, SeedDataConstants.AdminEmail);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        HttpResponseMessage createResponse = await adminClient.PostAsJsonAsync("/api/v1/admin/customers", new
        {
            name = $"Audit-Customer-{suffix}",
            isActive = true
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerPayload>(TestAuth.JsonOptions);
        Assert.NotNull(created);

        using IServiceScope scope = factory.Services.CreateScope();
        HelpdeskDbContext dbContext = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();

        bool hasAuditEvent = await dbContext.AuditEvents.AnyAsync(item =>
            item.EventType == "admin.customer.created" && item.CustomerId == created!.Id);

        Assert.True(hasAuditEvent);
    }

    private sealed record CustomerPayload(Guid Id, string Name, bool IsActive, int DomainCount);
}
