using System.Net;
using System.Net.Http.Json;
using Helpdesk.Light.Application.Contracts.Ai;
using Helpdesk.Light.Application.Contracts.Tickets;
using Helpdesk.Light.Domain.Ai;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;

namespace Helpdesk.Light.IntegrationTests;

public sealed class KnowledgeBaseIntegrationTests(HelpdeskApiFactory factory) : IClassFixture<HelpdeskApiFactory>
{
    [Fact]
    public async Task ResolvedTicket_CanGeneratePublishAndListKnowledgeArticle()
    {
        using HttpClient userClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(userClient, SeedDataConstants.ContosoEndUserEmail);

        TicketSummaryDto createdTicket = (await (await userClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            null,
            "VPN reconnect guide needed",
            "Users need a repeatable reconnect process.",
            TicketPriority.Medium))).Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;

        using HttpClient techClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(techClient, SeedDataConstants.ContosoTechEmail);

        HttpResponseMessage resolveResponse = await techClient.PostAsJsonAsync(
            $"/api/v1/tickets/{createdTicket.Id}/status",
            new TicketStatusUpdateRequest(TicketStatus.Resolved));

        resolveResponse.EnsureSuccessStatusCode();

        HttpResponseMessage generateResponse = await techClient.PostAsync($"/api/v1/knowledge/articles/from-ticket/{createdTicket.Id}", null);
        Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);

        KnowledgeArticleDetailDto draft = (await generateResponse.Content.ReadFromJsonAsync<KnowledgeArticleDetailDto>(TestAuth.JsonOptions))!;
        Assert.Equal(KnowledgeArticleStatus.Draft, draft.Status);
        Assert.Equal(createdTicket.Id, draft.SourceTicketId);
        Assert.True(draft.AiGenerated);

        HttpResponseMessage publishResponse = await techClient.PostAsync($"/api/v1/knowledge/articles/{draft.Id}/publish", null);
        publishResponse.EnsureSuccessStatusCode();

        KnowledgeArticleDetailDto published = (await publishResponse.Content.ReadFromJsonAsync<KnowledgeArticleDetailDto>(TestAuth.JsonOptions))!;
        Assert.Equal(KnowledgeArticleStatus.Published, published.Status);

        HttpResponseMessage listResponse = await techClient.GetAsync("/api/v1/knowledge/articles?status=Published&take=50");
        listResponse.EnsureSuccessStatusCode();

        List<KnowledgeArticleSummaryDto> list = (await listResponse.Content.ReadFromJsonAsync<List<KnowledgeArticleSummaryDto>>(TestAuth.JsonOptions))!;
        Assert.Contains(list, item => item.Id == draft.Id);
    }

    [Fact]
    public async Task Technician_CannotReadKnowledgeArticleOutsideTenant()
    {
        using HttpClient adminClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(adminClient, SeedDataConstants.AdminEmail);

        HttpResponseMessage createResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/knowledge/articles",
            new KnowledgeArticleDraftCreateRequest(
                SeedDataConstants.ContosoCustomerId,
                null,
                "Contoso-only Article",
                "Tenant-bound content",
                false));

        createResponse.EnsureSuccessStatusCode();
        KnowledgeArticleDetailDto created = (await createResponse.Content.ReadFromJsonAsync<KnowledgeArticleDetailDto>(TestAuth.JsonOptions))!;

        using HttpClient fabrikamTechClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(fabrikamTechClient, SeedDataConstants.FabrikamTechEmail);

        HttpResponseMessage forbidden = await fabrikamTechClient.GetAsync($"/api/v1/knowledge/articles/{created.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
