using System.Net;
using System.Net.Http.Json;
using Helpdesk.Light.Application.Contracts.Tickets;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;
using Helpdesk.Light.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Light.IntegrationTests;

public sealed class TicketLifecycleIntegrationTests(HelpdeskApiFactory factory) : IClassFixture<HelpdeskApiFactory>
{
    [Fact]
    public async Task EndUser_CanCreateViewAndReplyToTicket()
    {
        using HttpClient client = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(client, SeedDataConstants.ContosoEndUserEmail);

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            null,
            "Laptop cannot connect to Wi-Fi",
            "Connection drops every 5 minutes.",
            TicketPriority.Medium));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        TicketSummaryDto created = (await createResponse.Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;
        Assert.Equal(TicketStatus.New, created.Status);

        HttpResponseMessage listResponse = await client.GetAsync("/api/v1/tickets?take=50");
        listResponse.EnsureSuccessStatusCode();

        List<TicketSummaryDto> list = (await listResponse.Content.ReadFromJsonAsync<List<TicketSummaryDto>>(TestAuth.JsonOptions))!;
        Assert.Contains(list, item => item.Id == created.Id);

        HttpResponseMessage detailResponse = await client.GetAsync($"/api/v1/tickets/{created.Id}");
        detailResponse.EnsureSuccessStatusCode();

        TicketDetailDto detail = (await detailResponse.Content.ReadFromJsonAsync<TicketDetailDto>(TestAuth.JsonOptions))!;
        Assert.Single(detail.Messages);
        Assert.NotNull(detail.LatestAiSuggestion);

        HttpResponseMessage replyResponse = await client.PostAsJsonAsync($"/api/v1/tickets/{created.Id}/messages", new TicketMessageCreateRequest("Adding more details about this issue."));
        replyResponse.EnsureSuccessStatusCode();

        TicketDetailDto afterReply = (await (await client.GetAsync($"/api/v1/tickets/{created.Id}")).Content.ReadFromJsonAsync<TicketDetailDto>(TestAuth.JsonOptions))!;
        Assert.Equal(2, afterReply.Messages.Count);
        Assert.True(afterReply.Messages[0].CreatedUtc <= afterReply.Messages[1].CreatedUtc);
    }

    [Fact]
    public async Task Technician_CanAssignAndTransitionTicket()
    {
        using HttpClient userClient = factory.CreateClient();
        TestAuth.LoginResponse endUserLogin = await TestAuth.LoginAndSetAuthHeaderAsync(userClient, SeedDataConstants.ContosoEndUserEmail);

        TicketSummaryDto created = (await (await userClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            null,
            "Printer queue blocked",
            "Jobs are stuck in queue.",
            TicketPriority.Low))).Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;

        using HttpClient techClient = factory.CreateClient();
        TestAuth.LoginResponse techLogin = await TestAuth.LoginAndSetAuthHeaderAsync(techClient, SeedDataConstants.ContosoTechEmail);

        HttpResponseMessage assignResponse = await techClient.PostAsJsonAsync($"/api/v1/tickets/{created.Id}/assign", new TicketAssignRequest(techLogin.UserId));
        assignResponse.EnsureSuccessStatusCode();

        HttpResponseMessage triageResponse = await techClient.PostAsJsonAsync($"/api/v1/tickets/{created.Id}/triage", new TicketTriageUpdateRequest(TicketPriority.High, "ServiceIncident"));
        triageResponse.EnsureSuccessStatusCode();

        HttpResponseMessage statusResponse = await techClient.PostAsJsonAsync($"/api/v1/tickets/{created.Id}/status", new TicketStatusUpdateRequest(TicketStatus.InProgress));
        statusResponse.EnsureSuccessStatusCode();

        TicketSummaryDto updated = (await statusResponse.Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;
        Assert.Equal(TicketStatus.InProgress, updated.Status);

        TicketDetailDto detail = (await (await techClient.GetAsync($"/api/v1/tickets/{created.Id}")).Content.ReadFromJsonAsync<TicketDetailDto>(TestAuth.JsonOptions))!;
        Assert.Equal(TicketPriority.High, detail.Ticket.Priority);
        Assert.Equal("ServiceIncident", detail.Ticket.Category);
        Assert.Equal(techLogin.UserId, detail.Ticket.AssignedToUserId);
    }

    [Fact]
    public async Task EndUser_CannotReadOtherTenantTicket()
    {
        using HttpClient contosoClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(contosoClient, SeedDataConstants.ContosoEndUserEmail);

        TicketSummaryDto created = (await (await contosoClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            null,
            "Need help with Teams",
            "Audio not working.",
            TicketPriority.Medium))).Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;

        using HttpClient fabrikamClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(fabrikamClient, SeedDataConstants.FabrikamEndUserEmail);

        HttpResponseMessage forbidden = await fabrikamClient.GetAsync($"/api/v1/tickets/{created.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task PublicCreate_WithMappedDomain_CreatesTicketAndEndUserAccount()
    {
        using HttpClient publicClient = factory.CreateClient();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        string email = $"newuser-{suffix}@contoso.com";

        HttpResponseMessage createResponse = await publicClient.PostAsJsonAsync("/api/v1/tickets/public", new CreateTicketRequest(
            null,
            "Public submission issue",
            "Submitting from unauthenticated flow.",
            TicketPriority.Medium,
            email));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        TicketSummaryDto created = (await createResponse.Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;
        Assert.Equal(SeedDataConstants.ContosoCustomerId, created.CustomerId);

        using IServiceScope scope = factory.Services.CreateScope();
        HelpdeskDbContext dbContext = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();

        ApplicationUser? createdUser = await dbContext.Users.SingleOrDefaultAsync(item => item.Email == email);
        Assert.NotNull(createdUser);
        Assert.Equal(SeedDataConstants.ContosoCustomerId, createdUser!.CustomerId);

        Ticket? ticket = await dbContext.Tickets.SingleOrDefaultAsync(item => item.Id == created.Id);
        Assert.NotNull(ticket);
        Assert.Equal(createdUser.Id, ticket!.CreatedByUserId);
    }

    [Fact]
    public async Task PublicCreate_WithUnmappedDomain_AutoCreatesCustomerAndEndUser()
    {
        using HttpClient publicClient = factory.CreateClient();
        string domain = $"unknown-{Guid.NewGuid():N}.io";
        string email = $"someone@{domain}";

        HttpResponseMessage createResponse = await publicClient.PostAsJsonAsync("/api/v1/tickets/public", new CreateTicketRequest(
            null,
            "Unknown domain ticket",
            "This should auto-provision a new customer from email domain.",
            TicketPriority.Medium,
            email));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        TicketSummaryDto created = (await createResponse.Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;

        using IServiceScope scope = factory.Services.CreateScope();
        HelpdeskDbContext dbContext = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();

        var customer = await dbContext.Customers
            .Include(item => item.Domains)
            .SingleOrDefaultAsync(item => item.Id == created.CustomerId);

        Assert.NotNull(customer);
        Assert.Equal(domain, customer!.Name);
        Assert.Contains(customer.Domains, item => item.Domain == domain && item.IsPrimary);

        ApplicationUser? createdUser = await dbContext.Users.SingleOrDefaultAsync(item => item.Email == email);
        Assert.NotNull(createdUser);
        Assert.Equal(customer.Id, createdUser!.CustomerId);
    }

    [Fact]
    public async Task TechnicianCreate_WithCrossTenantEndUserEmail_IsForbidden()
    {
        using HttpClient technicianClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(technicianClient, SeedDataConstants.ContosoTechEmail);

        HttpResponseMessage createResponse = await technicianClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            null,
            "Cross tenant test",
            "Technician should not create outside tenant scope.",
            TicketPriority.Medium,
            "user@fabrikam.com"));

        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task TechnicianCreate_WithMappedEndUserEmail_CreatesTicketForProvisionedUser()
    {
        using HttpClient technicianClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(technicianClient, SeedDataConstants.ContosoTechEmail);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        string email = $"tech-created-{suffix}@contoso.com";

        HttpResponseMessage createResponse = await technicianClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            null,
            "Technician-created ticket",
            "Created on behalf of end user.",
            TicketPriority.High,
            email));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        TicketSummaryDto created = (await createResponse.Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;
        Assert.Equal(SeedDataConstants.ContosoCustomerId, created.CustomerId);

        using IServiceScope scope = factory.Services.CreateScope();
        HelpdeskDbContext dbContext = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
        ApplicationUser? endUser = await dbContext.Users.SingleOrDefaultAsync(item => item.Email == email);
        Assert.NotNull(endUser);

        Ticket? ticket = await dbContext.Tickets.SingleOrDefaultAsync(item => item.Id == created.Id);
        Assert.NotNull(ticket);
        Assert.Equal(endUser!.Id, ticket!.CreatedByUserId);
    }
}
