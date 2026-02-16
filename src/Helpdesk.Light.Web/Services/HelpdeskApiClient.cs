using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Contracts.Ai;
using Helpdesk.Light.Application.Contracts.Email;
using Helpdesk.Light.Application.Contracts.Tickets;

namespace Helpdesk.Light.Web.Services;

public sealed class HelpdeskApiClient(HttpClient httpClient, ClientSession session)
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await httpClient.PostAsJsonAsync("api/v1/auth/login", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
    }

    public async Task<AnalyticsDashboardDto> GetDashboardAsync(AnalyticsDashboardRequest request, CancellationToken cancellationToken = default)
    {
        string query =
            $"customerId={request.CustomerId}" +
            $"&fromUtc={FormatDate(request.FromUtc)}" +
            $"&toUtc={FormatDate(request.ToUtc)}";

        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, $"api/v1/analytics/dashboard?{query}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<AnalyticsDashboardDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<OperationsMetricsDto> GetOperationsMetricsAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, "api/v1/ops/metrics");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<OperationsMetricsDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<IReadOnlyList<DeadLetterMessageDto>> ListDeadLettersAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, $"api/v1/ops/dead-letters?take={Math.Clamp(take, 1, 500)}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<DeadLetterMessageDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<TicketSummaryDto>> ListTicketsAsync(TicketFilterRequest request, CancellationToken cancellationToken = default)
    {
        string query = $"status={request.Status}&priority={request.Priority}&customerId={request.CustomerId}&assignedToUserId={request.AssignedToUserId}&take={request.Take}";
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, $"api/v1/tickets?{query}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<TicketSummaryDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<TicketDetailDto?> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, $"api/v1/tickets/{ticketId}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TicketDetailDto>(cancellationToken: cancellationToken);
    }

    public async Task<TicketSummaryDto> CreateTicketAsync(CreateTicketRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, "api/v1/tickets");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<TicketSummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<TicketSummaryDto> CreatePublicTicketAsync(CreateTicketRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync("api/v1/tickets/public", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<TicketSummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<TicketMessageDto> AddMessageAsync(Guid ticketId, TicketMessageCreateRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/tickets/{ticketId}/messages");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<TicketMessageDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<TicketSummaryDto> AssignAsync(Guid ticketId, TicketAssignRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/tickets/{ticketId}/assign");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<TicketSummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<ResolverAssignmentOptionsDto> GetResolverAssignmentOptionsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, $"api/v1/resolver-groups/options?customerId={customerId}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<ResolverAssignmentOptionsDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<IReadOnlyList<ResolverGroupSummaryDto>> ListResolverGroupsAsync(Guid? customerId = null, CancellationToken cancellationToken = default)
    {
        string query = customerId.HasValue ? $"?customerId={customerId.Value}" : string.Empty;
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, $"api/v1/resolver-groups{query}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<ResolverGroupSummaryDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<ResolverGroupSummaryDto> CreateResolverGroupAsync(CreateResolverGroupRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, "api/v1/resolver-groups");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ResolverGroupSummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<ResolverGroupSummaryDto> UpdateResolverGroupAsync(Guid resolverGroupId, UpdateResolverGroupRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Put, $"api/v1/resolver-groups/{resolverGroupId}");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ResolverGroupSummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task DeleteResolverGroupAsync(Guid resolverGroupId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Delete, $"api/v1/resolver-groups/{resolverGroupId}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<TicketCategorySummaryDto>> ListTicketCategoriesAsync(Guid? customerId = null, CancellationToken cancellationToken = default)
    {
        string query = customerId.HasValue ? $"?customerId={customerId.Value}" : string.Empty;
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, $"api/v1/ticket-categories{query}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<TicketCategorySummaryDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<TicketCategorySummaryDto> CreateTicketCategoryAsync(CreateTicketCategoryRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, "api/v1/ticket-categories");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<TicketCategorySummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<TicketCategorySummaryDto> UpdateTicketCategoryAsync(Guid ticketCategoryId, UpdateTicketCategoryRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Put, $"api/v1/ticket-categories/{ticketCategoryId}");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<TicketCategorySummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task DeleteTicketCategoryAsync(Guid ticketCategoryId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Delete, $"api/v1/ticket-categories/{ticketCategoryId}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<TicketSummaryDto> UpdateStatusAsync(Guid ticketId, TicketStatusUpdateRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/tickets/{ticketId}/status");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<TicketSummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<TicketSummaryDto> UpdateTriageAsync(Guid ticketId, TicketTriageUpdateRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/tickets/{ticketId}/triage");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<TicketSummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<TicketAttachmentDto> UploadAttachmentAsync(Guid ticketId, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/tickets/{ticketId}/attachments");
        using MultipartFormDataContent form = new();
        using StreamContent streamContent = new(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(streamContent, "file", fileName);
        message.Content = form;

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<TicketAttachmentDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<AiRunResult> RunAiAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/tickets/{ticketId}/ai/run");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<AiRunResult>(cancellationToken: cancellationToken))!;
    }

    public async Task<AiRunResult?> ApproveAiAsync(Guid ticketId, TicketAiApprovalRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/tickets/{ticketId}/ai/approve-response");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AiRunResult>(cancellationToken: cancellationToken);
    }

    public async Task<AiRunResult?> DiscardAiAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/tickets/{ticketId}/ai/discard-response");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<AiRunResult>(cancellationToken: cancellationToken);
    }

    public async Task<InboundEmailProcessResult> ProcessInboundDevEmailAsync(InboundEmailRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, "api/v1/email/inbound/dev");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return (await response.Content.ReadFromJsonAsync<InboundEmailProcessResult>(cancellationToken: cancellationToken))!;
    }

    public async Task<PlatformSettingsDto> GetPlatformSettingsAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, "api/v1/admin/settings");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<PlatformSettingsDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<PlatformSettingsDto> UpdatePlatformSettingsAsync(PlatformSettingsUpdateRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Put, "api/v1/admin/settings");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<PlatformSettingsDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<IReadOnlyList<CustomerSummaryDto>> ListCustomersAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, "api/v1/admin/customers");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<CustomerSummaryDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<CustomerSummaryDto> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, "api/v1/admin/customers");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<CustomerSummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<CustomerSummaryDto> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Put, $"api/v1/admin/customers/{customerId}");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<CustomerSummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task DeleteCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Delete, $"api/v1/admin/customers/{customerId}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<CustomerDomainDto> AddCustomerDomainAsync(Guid customerId, AddCustomerDomainRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/admin/customers/{customerId}/domains");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<CustomerDomainDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<IReadOnlyList<EndUserSummaryDto>> ListCustomerEndUsersAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, $"api/v1/admin/customers/{customerId}/end-users");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<EndUserSummaryDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<EndUserSummaryDto> CreateCustomerEndUserAsync(Guid customerId, CreateEndUserRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/admin/customers/{customerId}/end-users");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<EndUserSummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<EndUserSummaryDto> UpdateCustomerEndUserAsync(Guid customerId, Guid userId, UpdateEndUserRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Put, $"api/v1/admin/customers/{customerId}/end-users/{userId}");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<EndUserSummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task DeleteCustomerEndUserAsync(Guid customerId, Guid userId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Delete, $"api/v1/admin/customers/{customerId}/end-users/{userId}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task UpdateCustomerAiPolicyAsync(Guid customerId, CustomerAiPolicyUpdateRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Patch, $"api/v1/admin/customers/{customerId}/ai-policy");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<OutboundEmailDto>> ListOutboundEmailAsync(Guid? customerId = null, CancellationToken cancellationToken = default)
    {
        string query = customerId.HasValue ? $"?customerId={customerId}" : string.Empty;
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, $"api/v1/email/outbound{query}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<OutboundEmailDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task DispatchOutboundAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, "api/v1/email/outbound/dispatch");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeArticleSummaryDto>> ListKnowledgeArticlesAsync(KnowledgeArticleListRequest request, CancellationToken cancellationToken = default)
    {
        string query =
            $"search={Uri.EscapeDataString(request.Search ?? string.Empty)}" +
            $"&status={request.Status}" +
            $"&customerId={request.CustomerId}" +
            $"&take={request.Take}";

        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, $"api/v1/knowledge/articles?{query}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<KnowledgeArticleSummaryDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<KnowledgeArticleDetailDto?> GetKnowledgeArticleAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, $"api/v1/knowledge/articles/{articleId}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<KnowledgeArticleDetailDto>(cancellationToken: cancellationToken);
    }

    public async Task<KnowledgeArticleDetailDto> CreateKnowledgeDraftAsync(KnowledgeArticleDraftCreateRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, "api/v1/knowledge/articles");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<KnowledgeArticleDetailDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<KnowledgeArticleDetailDto> UpdateKnowledgeDraftAsync(Guid articleId, KnowledgeArticleDraftUpdateRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Put, $"api/v1/knowledge/articles/{articleId}");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<KnowledgeArticleDetailDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<KnowledgeArticleDetailDto> GenerateKnowledgeDraftFromTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/knowledge/articles/from-ticket/{ticketId}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<KnowledgeArticleDetailDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<KnowledgeArticleDetailDto> PublishKnowledgeArticleAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/knowledge/articles/{articleId}/publish");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<KnowledgeArticleDetailDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<KnowledgeArticleDetailDto> ArchiveKnowledgeArticleAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/knowledge/articles/{articleId}/archive");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<KnowledgeArticleDetailDto>(cancellationToken: cancellationToken))!;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri)
    {
        HttpRequestMessage request = new(method, uri);
        if (!string.IsNullOrWhiteSpace(session.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }

        return request;
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue
            ? Uri.EscapeDataString(value.Value.ToUniversalTime().ToString("O"))
            : string.Empty;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? details = await TryReadErrorDetailsAsync(response, cancellationToken);
        string message = $"{(int)response.StatusCode} ({response.StatusCode})";
        if (!string.IsNullOrWhiteSpace(details))
        {
            message = $"{message}: {details}";
        }

        throw new InvalidOperationException(message);
    }

    private static async Task<string?> TryReadErrorDetailsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return response.ReasonPhrase;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }

            if (root.TryGetProperty("detail", out JsonElement detail) && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString();
            }

            if (root.TryGetProperty("title", out JsonElement title) && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString();
            }

            if (root.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in errors.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in property.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                            {
                                return item.GetString();
                            }
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Treat non-JSON response payloads as plain text below.
        }

        return content.Length <= 300 ? content : $"{content[..300]}...";
    }
}
