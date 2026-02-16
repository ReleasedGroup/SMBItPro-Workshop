using System.Net;
using System.Net.Http.Json;
using Helpdesk.Light.Application.Contracts.Ai;
using Helpdesk.Light.Application.Contracts.Tickets;
using Helpdesk.Light.Domain.Ai;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;

namespace Helpdesk.Light.IntegrationTests;

public sealed class EndToEndWorkflowIntegrationTests(HelpdeskApiFactory factory) : IClassFixture<HelpdeskApiFactory>
{
    [Fact]
    public async Task LoginToResolutionAndKnowledgePublish_Workflow_Completes()
    {
        using HttpClient endUserClient = factory.CreateClient();
        TestAuth.LoginResponse endUser = await TestAuth.LoginAndSetAuthHeaderAsync(endUserClient, SeedDataConstants.ContosoEndUserEmail);

        HttpResponseMessage createResponse = await endUserClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            null,
            "End-to-end workflow validation",
            "Need a full support lifecycle validation.",
            TicketPriority.Medium));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        TicketSummaryDto createdTicket = (await createResponse.Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;
        Assert.Equal(TicketStatus.New, createdTicket.Status);

        using HttpClient techClient = factory.CreateClient();
        TestAuth.LoginResponse technician = await TestAuth.LoginAndSetAuthHeaderAsync(techClient, SeedDataConstants.ContosoTechEmail);

        HttpResponseMessage assignResponse = await techClient.PostAsJsonAsync($"/api/v1/tickets/{createdTicket.Id}/assign", new TicketAssignRequest(technician.UserId));
        assignResponse.EnsureSuccessStatusCode();

        HttpResponseMessage inProgressResponse = await techClient.PostAsJsonAsync(
            $"/api/v1/tickets/{createdTicket.Id}/status",
            new TicketStatusUpdateRequest(TicketStatus.InProgress));
        inProgressResponse.EnsureSuccessStatusCode();

        HttpResponseMessage replyResponse = await techClient.PostAsJsonAsync(
            $"/api/v1/tickets/{createdTicket.Id}/messages",
            new TicketMessageCreateRequest("Technician investigating and applying fix."));
        replyResponse.EnsureSuccessStatusCode();

        HttpResponseMessage resolveResponse = await techClient.PostAsJsonAsync(
            $"/api/v1/tickets/{createdTicket.Id}/status",
            new TicketStatusUpdateRequest(TicketStatus.Resolved));
        resolveResponse.EnsureSuccessStatusCode();

        TicketSummaryDto resolved = (await resolveResponse.Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;
        Assert.Equal(TicketStatus.Resolved, resolved.Status);

        HttpResponseMessage draftResponse = await techClient.PostAsync($"/api/v1/knowledge/articles/from-ticket/{createdTicket.Id}", null);
        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);

        KnowledgeArticleDetailDto draft = (await draftResponse.Content.ReadFromJsonAsync<KnowledgeArticleDetailDto>(TestAuth.JsonOptions))!;
        Assert.Equal(KnowledgeArticleStatus.Draft, draft.Status);

        HttpResponseMessage publishResponse = await techClient.PostAsync($"/api/v1/knowledge/articles/{draft.Id}/publish", null);
        publishResponse.EnsureSuccessStatusCode();

        KnowledgeArticleDetailDto published = (await publishResponse.Content.ReadFromJsonAsync<KnowledgeArticleDetailDto>(TestAuth.JsonOptions))!;
        Assert.Equal(KnowledgeArticleStatus.Published, published.Status);

        HttpResponseMessage detailResponse = await endUserClient.GetAsync($"/api/v1/tickets/{createdTicket.Id}");
        detailResponse.EnsureSuccessStatusCode();

        TicketDetailDto detail = (await detailResponse.Content.ReadFromJsonAsync<TicketDetailDto>(TestAuth.JsonOptions))!;
        Assert.Contains(detail.Messages, item => item.AuthorType == TicketAuthorType.Technician);
        Assert.Equal(TicketStatus.Resolved, detail.Ticket.Status);
        Assert.Equal(endUser.UserId, detail.Ticket.CreatedByUserId);
    }
}
