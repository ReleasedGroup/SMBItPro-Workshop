using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

    public async Task<IReadOnlyList<TicketSummaryDto>> ListTicketsAsync(TicketFilterRequest request, CancellationToken cancellationToken = default)
    {
        string query = $"status={request.Status}&priority={request.Priority}&customerId={request.CustomerId}&assignedToUserId={request.AssignedToUserId}&take={request.Take}";
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, $"api/v1/tickets?{query}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

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

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TicketDetailDto>(cancellationToken: cancellationToken);
    }

    public async Task<TicketSummaryDto> CreateTicketAsync(CreateTicketRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, "api/v1/tickets");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TicketSummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<TicketMessageDto> AddMessageAsync(Guid ticketId, TicketMessageCreateRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/tickets/{ticketId}/messages");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TicketMessageDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<TicketSummaryDto> AssignAsync(Guid ticketId, TicketAssignRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/tickets/{ticketId}/assign");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TicketSummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<TicketSummaryDto> UpdateStatusAsync(Guid ticketId, TicketStatusUpdateRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/tickets/{ticketId}/status");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TicketSummaryDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<TicketSummaryDto> UpdateTriageAsync(Guid ticketId, TicketTriageUpdateRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/tickets/{ticketId}/triage");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

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
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TicketAttachmentDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<AiRunResult> RunAiAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, $"api/v1/tickets/{ticketId}/ai/run");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

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

        response.EnsureSuccessStatusCode();
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

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiRunResult>(cancellationToken: cancellationToken);
    }

    public async Task<InboundEmailProcessResult> ProcessInboundDevEmailAsync(InboundEmailRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, "api/v1/email/inbound/dev");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<InboundEmailProcessResult>(cancellationToken: cancellationToken))!;
    }

    public async Task<IReadOnlyList<CustomerSummaryDto>> ListCustomersAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, "api/v1/admin/customers");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<CustomerSummaryDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task UpdateCustomerAiPolicyAsync(Guid customerId, CustomerAiPolicyUpdateRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Patch, $"api/v1/admin/customers/{customerId}/ai-policy");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<OutboundEmailDto>> ListOutboundEmailAsync(Guid? customerId = null, CancellationToken cancellationToken = default)
    {
        string query = customerId.HasValue ? $"?customerId={customerId}" : string.Empty;
        using HttpRequestMessage message = CreateRequest(HttpMethod.Get, $"api/v1/email/outbound{query}");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<OutboundEmailDto>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task DispatchOutboundAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage message = CreateRequest(HttpMethod.Post, "api/v1/email/outbound/dispatch");
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
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
}
