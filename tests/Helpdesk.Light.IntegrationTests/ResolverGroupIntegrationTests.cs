using System.Net;
using System.Net.Http.Json;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Contracts.Tickets;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;

namespace Helpdesk.Light.IntegrationTests;

public sealed class ResolverGroupIntegrationTests(HelpdeskApiFactory factory) : IClassFixture<HelpdeskApiFactory>
{
    [Fact]
    public async Task Technician_CanCrudResolverGroup_And_AssignTicketToGroup()
    {
        using HttpClient endUserClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(endUserClient, SeedDataConstants.ContosoEndUserEmail);

        TicketSummaryDto createdTicket = (await (await endUserClient.PostAsJsonAsync(
            "/api/v1/tickets",
            new CreateTicketRequest(null, "Resolver group assignment", "Need group routing validation.", TicketPriority.Medium)))
            .Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;

        using HttpClient techClient = factory.CreateClient();
        TestAuth.LoginResponse technician = await TestAuth.LoginAndSetAuthHeaderAsync(techClient, SeedDataConstants.ContosoTechEmail);

        HttpResponseMessage createGroupResponse = await techClient.PostAsJsonAsync(
            "/api/v1/resolver-groups",
            new CreateResolverGroupRequest(SeedDataConstants.ContosoCustomerId, "Tier 1", true));
        createGroupResponse.EnsureSuccessStatusCode();

        ResolverGroupSummaryDto group = (await createGroupResponse.Content.ReadFromJsonAsync<ResolverGroupSummaryDto>(TestAuth.JsonOptions))!;

        ResolverAssignmentOptionsDto options = (await (await techClient.GetAsync(
            $"/api/v1/resolver-groups/options?customerId={SeedDataConstants.ContosoCustomerId}"))
            .Content.ReadFromJsonAsync<ResolverAssignmentOptionsDto>(TestAuth.JsonOptions))!;

        Assert.Contains(options.Users, item => item.Id == technician.UserId);
        Assert.Contains(options.Groups, item => item.Id == group.Id);

        HttpResponseMessage assignGroupResponse = await techClient.PostAsJsonAsync(
            $"/api/v1/tickets/{createdTicket.Id}/assign",
            new TicketAssignRequest(null, group.Id));
        assignGroupResponse.EnsureSuccessStatusCode();

        TicketDetailDto assignedToGroup = (await (await techClient.GetAsync($"/api/v1/tickets/{createdTicket.Id}"))
            .Content.ReadFromJsonAsync<TicketDetailDto>(TestAuth.JsonOptions))!;
        Assert.Equal(group.Id, assignedToGroup.Ticket.ResolverGroupId);
        Assert.Null(assignedToGroup.Ticket.AssignedToUserId);

        HttpResponseMessage assignUserResponse = await techClient.PostAsJsonAsync(
            $"/api/v1/tickets/{createdTicket.Id}/assign",
            new TicketAssignRequest(technician.UserId));
        assignUserResponse.EnsureSuccessStatusCode();

        TicketDetailDto assignedToUser = (await (await techClient.GetAsync($"/api/v1/tickets/{createdTicket.Id}"))
            .Content.ReadFromJsonAsync<TicketDetailDto>(TestAuth.JsonOptions))!;
        Assert.Equal(technician.UserId, assignedToUser.Ticket.AssignedToUserId);
        Assert.Null(assignedToUser.Ticket.ResolverGroupId);

        HttpResponseMessage updateGroupResponse = await techClient.PutAsJsonAsync(
            $"/api/v1/resolver-groups/{group.Id}",
            new UpdateResolverGroupRequest("Tier 1 Updated", false));
        updateGroupResponse.EnsureSuccessStatusCode();

        ResolverGroupSummaryDto updatedGroup = (await updateGroupResponse.Content.ReadFromJsonAsync<ResolverGroupSummaryDto>(TestAuth.JsonOptions))!;
        Assert.Equal("Tier 1 Updated", updatedGroup.Name);
        Assert.False(updatedGroup.IsActive);

        HttpResponseMessage deleteGroupResponse = await techClient.DeleteAsync($"/api/v1/resolver-groups/{group.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteGroupResponse.StatusCode);
    }

    [Fact]
    public async Task Assignment_ToOtherCustomerResolverGroup_IsRejected()
    {
        using HttpClient endUserClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(endUserClient, SeedDataConstants.ContosoEndUserEmail);

        TicketSummaryDto createdTicket = (await (await endUserClient.PostAsJsonAsync(
            "/api/v1/tickets",
            new CreateTicketRequest(null, "Cross-tenant group assignment", "Should be blocked.", TicketPriority.Low)))
            .Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;

        using HttpClient adminClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(adminClient, SeedDataConstants.AdminEmail);

        ResolverGroupSummaryDto fabrikamGroup = (await (await adminClient.PostAsJsonAsync(
            "/api/v1/resolver-groups",
            new CreateResolverGroupRequest(SeedDataConstants.FabrikamCustomerId, "Fabrikam Tier 1", true)))
            .Content.ReadFromJsonAsync<ResolverGroupSummaryDto>(TestAuth.JsonOptions))!;

        using HttpClient techClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(techClient, SeedDataConstants.ContosoTechEmail);

        HttpResponseMessage assignResponse = await techClient.PostAsJsonAsync(
            $"/api/v1/tickets/{createdTicket.Id}/assign",
            new TicketAssignRequest(null, fabrikamGroup.Id));

        Assert.Equal(HttpStatusCode.BadRequest, assignResponse.StatusCode);
    }

    [Fact]
    public async Task ResolverGroup_WithAssignedTickets_CannotBeDeleted()
    {
        using HttpClient endUserClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(endUserClient, SeedDataConstants.ContosoEndUserEmail);

        TicketSummaryDto createdTicket = (await (await endUserClient.PostAsJsonAsync(
            "/api/v1/tickets",
            new CreateTicketRequest(null, "Delete guard", "Group delete should fail while assigned.", TicketPriority.Medium)))
            .Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;

        using HttpClient techClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(techClient, SeedDataConstants.ContosoTechEmail);

        ResolverGroupSummaryDto group = (await (await techClient.PostAsJsonAsync(
            "/api/v1/resolver-groups",
            new CreateResolverGroupRequest(SeedDataConstants.ContosoCustomerId, "Escalation", true)))
            .Content.ReadFromJsonAsync<ResolverGroupSummaryDto>(TestAuth.JsonOptions))!;

        HttpResponseMessage assignResponse = await techClient.PostAsJsonAsync(
            $"/api/v1/tickets/{createdTicket.Id}/assign",
            new TicketAssignRequest(null, group.Id));
        assignResponse.EnsureSuccessStatusCode();

        HttpResponseMessage deleteResponse = await techClient.DeleteAsync($"/api/v1/resolver-groups/{group.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
    }
}
