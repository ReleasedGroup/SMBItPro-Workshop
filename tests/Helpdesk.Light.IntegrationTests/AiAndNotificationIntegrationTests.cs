using System.Net;
using System.Net.Http.Json;
using Helpdesk.Light.Application.Contracts.Ai;
using Helpdesk.Light.Application.Contracts.Email;
using Helpdesk.Light.Application.Contracts.Tickets;
using Helpdesk.Light.Domain.Ai;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;

namespace Helpdesk.Light.IntegrationTests;

public sealed class AiAndNotificationIntegrationTests(HelpdeskApiFactory factory) : IClassFixture<HelpdeskApiFactory>
{
    [Fact]
    public async Task TicketCreation_ProducesPendingAiSuggestion_InSuggestOnlyMode()
    {
        using HttpClient userClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(userClient, SeedDataConstants.ContosoEndUserEmail);

        TicketSummaryDto created = (await (await userClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            null,
            "Need help accessing Teams",
            "My Teams app signs out repeatedly.",
            TicketPriority.Medium))).Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;

        TicketDetailDto detail = (await (await userClient.GetAsync($"/api/v1/tickets/{created.Id}")).Content.ReadFromJsonAsync<TicketDetailDto>(TestAuth.JsonOptions))!;

        Assert.NotNull(detail.LatestAiSuggestion);
        Assert.Equal(AiSuggestionStatus.PendingApproval, detail.LatestAiSuggestion!.Status);
    }

    [Fact]
    public async Task AutoRespondLowRiskMode_SendsAiMessageAutomatically()
    {
        using HttpClient adminClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(adminClient, SeedDataConstants.AdminEmail);

        HttpResponseMessage policyResponse = await adminClient.PatchAsJsonAsync(
            $"/api/v1/admin/customers/{SeedDataConstants.ContosoCustomerId}/ai-policy",
            new CustomerAiPolicyUpdateRequest(AiPolicyMode.AutoRespondLowRisk, 0.20));

        Assert.Equal(HttpStatusCode.NoContent, policyResponse.StatusCode);

        using HttpClient userClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(userClient, SeedDataConstants.ContosoEndUserEmail);

        TicketSummaryDto created = (await (await userClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            null,
            "Password reset needed",
            "Please reset my account password.",
            TicketPriority.Low))).Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;

        TicketDetailDto detail = (await (await userClient.GetAsync($"/api/v1/tickets/{created.Id}")).Content.ReadFromJsonAsync<TicketDetailDto>(TestAuth.JsonOptions))!;

        Assert.NotNull(detail.LatestAiSuggestion);
        Assert.Equal(AiSuggestionStatus.AutoSent, detail.LatestAiSuggestion!.Status);
        Assert.Contains(detail.Messages, item => item.AuthorType == TicketAuthorType.Agent && item.Source == TicketMessageSource.Ai);
    }

    [Fact]
    public async Task Technician_CanApproveAndDiscardAiSuggestions()
    {
        using HttpClient adminClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(adminClient, SeedDataConstants.AdminEmail);

        await adminClient.PatchAsJsonAsync(
            $"/api/v1/admin/customers/{SeedDataConstants.ContosoCustomerId}/ai-policy",
            new CustomerAiPolicyUpdateRequest(AiPolicyMode.SuggestOnly, 0.80));

        using HttpClient userClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(userClient, SeedDataConstants.ContosoEndUserEmail);

        TicketSummaryDto firstTicket = (await (await userClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            null,
            "Need VPN support",
            "VPN connection drops.",
            TicketPriority.Medium))).Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;

        TicketSummaryDto secondTicket = (await (await userClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            null,
            "Need Outlook support",
            "Outlook profile keeps asking for credentials.",
            TicketPriority.Medium))).Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;

        using HttpClient techClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(techClient, SeedDataConstants.ContosoTechEmail);

        HttpResponseMessage approveResponse = await techClient.PostAsJsonAsync(
            $"/api/v1/tickets/{firstTicket.Id}/ai/approve-response",
            new TicketAiApprovalRequest("Approved response text from technician."));

        approveResponse.EnsureSuccessStatusCode();

        TicketDetailDto approvedDetail = (await (await techClient.GetAsync($"/api/v1/tickets/{firstTicket.Id}")).Content.ReadFromJsonAsync<TicketDetailDto>(TestAuth.JsonOptions))!;
        Assert.Equal(AiSuggestionStatus.Approved, approvedDetail.LatestAiSuggestion!.Status);
        Assert.Contains(approvedDetail.Messages, item => item.AuthorType == TicketAuthorType.Agent && item.Body.Contains("Approved response text", StringComparison.Ordinal));

        HttpResponseMessage discardResponse = await techClient.PostAsync($"/api/v1/tickets/{secondTicket.Id}/ai/discard-response", null);
        discardResponse.EnsureSuccessStatusCode();

        TicketDetailDto discardedDetail = (await (await techClient.GetAsync($"/api/v1/tickets/{secondTicket.Id}")).Content.ReadFromJsonAsync<TicketDetailDto>(TestAuth.JsonOptions))!;
        Assert.Equal(AiSuggestionStatus.Discarded, discardedDetail.LatestAiSuggestion!.Status);
    }

    [Fact]
    public async Task TicketUpdates_GenerateOutboundEmailRecords()
    {
        using HttpClient userClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(userClient, SeedDataConstants.ContosoEndUserEmail);

        TicketSummaryDto created = (await (await userClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            null,
            "Need monitor replacement",
            "Monitor flickers frequently.",
            TicketPriority.Medium))).Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;

        await userClient.PostAsJsonAsync($"/api/v1/tickets/{created.Id}/messages", new TicketMessageCreateRequest("This is affecting work right now."));

        using HttpClient adminClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(adminClient, SeedDataConstants.AdminEmail);

        HttpResponseMessage outboundResponse = await adminClient.GetAsync("/api/v1/email/outbound");
        outboundResponse.EnsureSuccessStatusCode();

        List<OutboundEmailDto> outbound = (await outboundResponse.Content.ReadFromJsonAsync<List<OutboundEmailDto>>(TestAuth.JsonOptions))!;
        Assert.Contains(outbound, item => item.TicketId == created.Id);
        Assert.Contains(outbound, item => item.Status == "Sent");
    }
}
