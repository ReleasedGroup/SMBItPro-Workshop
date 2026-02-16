using System.Net;
using System.Net.Http.Json;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Contracts.Tickets;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;

namespace Helpdesk.Light.IntegrationTests;

public sealed class TicketCategoryIntegrationTests(HelpdeskApiFactory factory) : IClassFixture<HelpdeskApiFactory>
{
    [Fact]
    public async Task Technician_CanCrudTicketCategories_WithResolverGroupMapping()
    {
        using HttpClient techClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(techClient, SeedDataConstants.ContosoTechEmail);
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string groupName = $"Network Team {suffix}";
        string categoryName = $"Network {suffix}";
        string updatedCategoryName = $"Network & Connectivity {suffix}";

        ResolverGroupSummaryDto group = (await (await techClient.PostAsJsonAsync(
            "/api/v1/resolver-groups",
            new CreateResolverGroupRequest(SeedDataConstants.ContosoCustomerId, groupName, true)))
            .Content.ReadFromJsonAsync<ResolverGroupSummaryDto>(TestAuth.JsonOptions))!;

        HttpResponseMessage createResponse = await techClient.PostAsJsonAsync(
            "/api/v1/ticket-categories",
            new CreateTicketCategoryRequest(SeedDataConstants.ContosoCustomerId, categoryName, true, group.Id));
        createResponse.EnsureSuccessStatusCode();

        TicketCategorySummaryDto created = (await createResponse.Content.ReadFromJsonAsync<TicketCategorySummaryDto>(TestAuth.JsonOptions))!;
        Assert.Equal(group.Id, created.ResolverGroupId);

        HttpResponseMessage listResponse = await techClient.GetAsync($"/api/v1/ticket-categories?customerId={SeedDataConstants.ContosoCustomerId}");
        listResponse.EnsureSuccessStatusCode();
        List<TicketCategorySummaryDto> list = (await listResponse.Content.ReadFromJsonAsync<List<TicketCategorySummaryDto>>(TestAuth.JsonOptions))!;
        Assert.Contains(list, item => item.Id == created.Id && item.Name == categoryName);

        HttpResponseMessage updateResponse = await techClient.PutAsJsonAsync(
            $"/api/v1/ticket-categories/{created.Id}",
            new UpdateTicketCategoryRequest(updatedCategoryName, true, null));
        updateResponse.EnsureSuccessStatusCode();

        TicketCategorySummaryDto updated = (await updateResponse.Content.ReadFromJsonAsync<TicketCategorySummaryDto>(TestAuth.JsonOptions))!;
        Assert.Equal(updatedCategoryName, updated.Name);
        Assert.Null(updated.ResolverGroupId);

        HttpResponseMessage deleteResponse = await techClient.DeleteAsync($"/api/v1/ticket-categories/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Triage_WithMappedCategory_AssignsResolverGroup()
    {
        using HttpClient endUserClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(endUserClient, SeedDataConstants.ContosoEndUserEmail);
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string groupName = $"Connectivity Team {suffix}";
        string categoryName = $"Network {suffix}";

        TicketSummaryDto createdTicket = (await (await endUserClient.PostAsJsonAsync(
            "/api/v1/tickets",
            new CreateTicketRequest(null, "Wifi issue", "Intermittent connectivity", TicketPriority.Medium)))
            .Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;

        using HttpClient techClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(techClient, SeedDataConstants.ContosoTechEmail);

        ResolverGroupSummaryDto group = (await (await techClient.PostAsJsonAsync(
            "/api/v1/resolver-groups",
            new CreateResolverGroupRequest(SeedDataConstants.ContosoCustomerId, groupName, true)))
            .Content.ReadFromJsonAsync<ResolverGroupSummaryDto>(TestAuth.JsonOptions))!;

        HttpResponseMessage createCategoryResponse = await techClient.PostAsJsonAsync(
            "/api/v1/ticket-categories",
            new CreateTicketCategoryRequest(SeedDataConstants.ContosoCustomerId, categoryName, true, group.Id));
        createCategoryResponse.EnsureSuccessStatusCode();

        HttpResponseMessage triageResponse = await techClient.PostAsJsonAsync(
            $"/api/v1/tickets/{createdTicket.Id}/triage",
            new TicketTriageUpdateRequest(TicketPriority.High, categoryName));
        triageResponse.EnsureSuccessStatusCode();

        TicketSummaryDto triaged = (await triageResponse.Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;
        Assert.Equal(group.Id, triaged.ResolverGroupId);
        Assert.Null(triaged.AssignedToUserId);

        TicketDetailDto detail = (await (await techClient.GetAsync($"/api/v1/tickets/{createdTicket.Id}"))
            .Content.ReadFromJsonAsync<TicketDetailDto>(TestAuth.JsonOptions))!;
        Assert.Equal(group.Id, detail.Ticket.ResolverGroupId);
    }

    [Fact]
    public async Task CategoryMapping_ToDifferentCustomerResolverGroup_IsRejected()
    {
        using HttpClient adminClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(adminClient, SeedDataConstants.AdminEmail);

        ResolverGroupSummaryDto fabrikamGroup = (await (await adminClient.PostAsJsonAsync(
            "/api/v1/resolver-groups",
            new CreateResolverGroupRequest(SeedDataConstants.FabrikamCustomerId, "Fabrikam Team", true)))
            .Content.ReadFromJsonAsync<ResolverGroupSummaryDto>(TestAuth.JsonOptions))!;

        using HttpClient techClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(techClient, SeedDataConstants.ContosoTechEmail);

        HttpResponseMessage createResponse = await techClient.PostAsJsonAsync(
            "/api/v1/ticket-categories",
            new CreateTicketCategoryRequest(SeedDataConstants.ContosoCustomerId, "Security", true, fabrikamGroup.Id));

        Assert.Equal(HttpStatusCode.Conflict, createResponse.StatusCode);
    }
}
