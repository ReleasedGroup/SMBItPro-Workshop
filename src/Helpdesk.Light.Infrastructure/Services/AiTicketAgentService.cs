using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Abstractions.Ai;
using Helpdesk.Light.Application.Abstractions.Email;
using Helpdesk.Light.Application.Abstractions.Tickets;
using Helpdesk.Light.Application.Contracts.Ai;
using Helpdesk.Light.Application.Errors;
using Helpdesk.Light.Domain.Ai;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;
using Helpdesk.Light.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class AiTicketAgentService : IAiTicketAgentService
{
    private static readonly HashSet<string> RestrictedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "SecurityIncident",
        "BillingDispute",
        "LegalRequest"
    };

    private readonly HelpdeskDbContext dbContext;
    private readonly ITenantContextAccessor tenantContextAccessor;
    private readonly ITicketAccessGuard ticketAccessGuard;
    private readonly IOutboundEmailService outboundEmailService;
    private readonly AiOptions aiOptions;
    private readonly ILogger<AiTicketAgentService> logger;
    private readonly Kernel? kernel;

    public AiTicketAgentService(
        HelpdeskDbContext dbContext,
        ITenantContextAccessor tenantContextAccessor,
        ITicketAccessGuard ticketAccessGuard,
        IOutboundEmailService outboundEmailService,
        IOptions<AiOptions> options,
        ILogger<AiTicketAgentService> logger)
    {
        this.dbContext = dbContext;
        this.tenantContextAccessor = tenantContextAccessor;
        this.ticketAccessGuard = ticketAccessGuard;
        this.outboundEmailService = outboundEmailService;
        this.aiOptions = options.Value;
        this.logger = logger;
        kernel = BuildKernel(aiOptions);
    }

    public async Task<AiRunResult> RunForTicketAsync(AiRunRequest request, CancellationToken cancellationToken = default)
    {
        Ticket ticket = await dbContext.Tickets.SingleOrDefaultAsync(item => item.Id == request.TicketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket '{request.TicketId}' was not found.");

        var customer = await dbContext.Customers.SingleAsync(item => item.Id == ticket.CustomerId, cancellationToken);
        List<TicketMessage> messages = await dbContext.TicketMessages
            .Where(item => item.TicketId == ticket.Id)
            .OrderByDescending(item => item.CreatedUtc)
            .Take(12)
            .ToListAsync(cancellationToken);

        List<KnowledgeArticle> articles = await dbContext.KnowledgeArticles
            .AsNoTracking()
            .Where(item => item.Status == "Published" && (item.CustomerId == null || item.CustomerId == ticket.CustomerId))
            .OrderByDescending(item => item.UpdatedUtc)
            .Take(3)
            .ToListAsync(cancellationToken);

        DateTime utcNow = DateTime.UtcNow;
        AiGeneration generated = await GenerateSuggestionAsync(ticket, messages, articles, cancellationToken);

        string mode = customer.AiPolicyMode.ToString();
        AiRun run = new(
            Guid.NewGuid(),
            ticket.Id,
            aiOptions.ModelId,
            mode,
            generated.PromptHash,
            generated.InputTokens,
            generated.OutputTokens,
            generated.Confidence,
            "Completed",
            utcNow);

        dbContext.AiRuns.Add(run);

        TicketAiSuggestion suggestion = new(
            Guid.NewGuid(),
            ticket.Id,
            generated.DraftResponse,
            generated.SuggestedCategory,
            generated.SuggestedPriority,
            generated.RiskLevel,
            generated.Confidence,
            AiSuggestionStatus.PendingApproval,
            utcNow);

        dbContext.TicketAiSuggestions.Add(suggestion);

        if (Enum.TryParse(generated.SuggestedPriority, true, out TicketPriority suggestedPriority))
        {
            ticket.SetPriority(suggestedPriority, utcNow);
        }

        ticket.SetCategory(generated.SuggestedCategory, utcNow);

        bool autoResponseSent = false;
        bool lowRisk = generated.RiskLevel.Equals("Low", StringComparison.OrdinalIgnoreCase);
        bool aboveThreshold = generated.Confidence >= customer.AutoRespondMinConfidence;
        bool restrictedCategory = RestrictedCategories.Contains(generated.SuggestedCategory);

        if (customer.AiPolicyMode == AiPolicyMode.AutoRespondLowRisk && lowRisk && aboveThreshold && !restrictedCategory)
        {
            suggestion.SetStatus(AiSuggestionStatus.AutoSent, null, utcNow);

            TicketMessage aiMessage = new(
                Guid.NewGuid(),
                ticket.Id,
                TicketAuthorType.Agent,
                null,
                suggestion.DraftResponse,
                TicketMessageSource.Ai,
                null,
                utcNow);

            dbContext.TicketMessages.Add(aiMessage);
            autoResponseSent = true;

            string? recipient = await dbContext.Users
                .Where(item => item.Id == ticket.CreatedByUserId)
                .Select(item => item.Email)
                .SingleOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(recipient))
            {
                await outboundEmailService.QueueAsync(
                    ticket.CustomerId,
                    ticket.Id,
                    recipient,
                    $"[{ticket.ReferenceCode}] AI response",
                    suggestion.DraftResponse,
                    $"ai-auto-response:{ticket.Id}:{suggestion.Id}",
                    cancellationToken);
            }

            AddAudit(ticket.CustomerId, null, "ai.auto_response.sent", new { ticketId = ticket.Id, suggestionId = suggestion.Id, generated.Confidence });
        }
        else
        {
            AddAudit(ticket.CustomerId, null, "ai.suggestion.created", new { ticketId = ticket.Id, suggestionId = suggestion.Id, generated.Confidence });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AiRunResult(
            ticket.Id,
            suggestion.SuggestedCategory,
            suggestion.SuggestedPriority,
            suggestion.DraftResponse,
            suggestion.RiskLevel,
            suggestion.Confidence,
            suggestion.Status,
            autoResponseSent);
    }

    public async Task<AiRunResult?> ApproveSuggestionAsync(Guid ticketId, TicketAiApprovalRequest request, CancellationToken cancellationToken = default)
    {
        Ticket ticket = await dbContext.Tickets.SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket '{ticketId}' was not found.");

        TenantAccessContext context = tenantContextAccessor.Current;
        if (!ticketAccessGuard.CanManage(ticket, context))
        {
            throw new TenantAccessDeniedException("Cannot approve AI suggestion outside tenant boundary.");
        }

        TicketAiSuggestion? suggestion = await dbContext.TicketAiSuggestions
            .Where(item => item.TicketId == ticketId && item.Status == AiSuggestionStatus.PendingApproval)
            .OrderByDescending(item => item.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (suggestion is null)
        {
            return null;
        }

        DateTime utcNow = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.EditedResponse))
        {
            suggestion.UpdateDraftResponse(request.EditedResponse, utcNow);
        }

        suggestion.SetStatus(AiSuggestionStatus.Approved, context.UserId, utcNow);

        TicketMessage aiMessage = new(
            Guid.NewGuid(),
            ticket.Id,
            TicketAuthorType.Agent,
            null,
            suggestion.DraftResponse,
            TicketMessageSource.Ai,
            null,
            utcNow);

        dbContext.TicketMessages.Add(aiMessage);
        AddAudit(ticket.CustomerId, context.UserId, "ai.suggestion.approved", new { ticketId = ticket.Id, suggestionId = suggestion.Id });

        await dbContext.SaveChangesAsync(cancellationToken);

        string? recipient = await dbContext.Users
            .Where(item => item.Id == ticket.CreatedByUserId)
            .Select(item => item.Email)
            .SingleOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(recipient))
        {
            await outboundEmailService.QueueAsync(
                ticket.CustomerId,
                ticket.Id,
                recipient,
                $"[{ticket.ReferenceCode}] Support response",
                suggestion.DraftResponse,
                $"ai-approved-response:{ticket.Id}:{suggestion.Id}",
                cancellationToken);
        }

        return ToResult(ticketId, suggestion, false);
    }

    public async Task<AiRunResult?> DiscardSuggestionAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        Ticket ticket = await dbContext.Tickets.SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket '{ticketId}' was not found.");

        TenantAccessContext context = tenantContextAccessor.Current;
        if (!ticketAccessGuard.CanManage(ticket, context))
        {
            throw new TenantAccessDeniedException("Cannot discard AI suggestion outside tenant boundary.");
        }

        TicketAiSuggestion? suggestion = await dbContext.TicketAiSuggestions
            .Where(item => item.TicketId == ticketId && item.Status == AiSuggestionStatus.PendingApproval)
            .OrderByDescending(item => item.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (suggestion is null)
        {
            return null;
        }

        suggestion.SetStatus(AiSuggestionStatus.Discarded, context.UserId, DateTime.UtcNow);
        AddAudit(ticket.CustomerId, context.UserId, "ai.suggestion.discarded", new { ticketId = ticket.Id, suggestionId = suggestion.Id });
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResult(ticketId, suggestion, false);
    }

    private static Kernel? BuildKernel(AiOptions options)
    {
        if (!options.EnableAi || string.IsNullOrWhiteSpace(options.OpenAIApiKey))
        {
            return null;
        }

        IKernelBuilder builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(options.ModelId, options.OpenAIApiKey);
        return builder.Build();
    }

    private async Task<AiGeneration> GenerateSuggestionAsync(
        Ticket ticket,
        IReadOnlyList<TicketMessage> messages,
        IReadOnlyList<KnowledgeArticle> articles,
        CancellationToken cancellationToken)
    {
        string prompt = BuildPrompt(ticket, messages, articles);
        string promptHash = ComputeSha256(prompt);

        if (kernel is null)
        {
            return BuildFallbackGeneration(ticket, messages, promptHash);
        }

        try
        {
            FunctionResult result = await kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
            string? raw = result.GetValue<string>();
            AiJsonPayload payload = ParsePayload(raw) ?? BuildFallbackPayload(ticket, messages);

            return new AiGeneration(
                payload.Category,
                payload.Priority,
                payload.Response,
                payload.Risk,
                payload.Confidence,
                EstimateTokens(prompt),
                EstimateTokens(raw ?? payload.Response),
                promptHash);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "AI prompt invocation failed; using fallback generation.");
            return BuildFallbackGeneration(ticket, messages, promptHash);
        }
    }

    private static string BuildPrompt(Ticket ticket, IReadOnlyList<TicketMessage> messages, IReadOnlyList<KnowledgeArticle> articles)
    {
        StringBuilder builder = new();
        builder.AppendLine("You are an MSP helpdesk agent. Produce a JSON object with keys:");
        builder.AppendLine("category, priority, response, risk, confidence");
        builder.AppendLine("priority must be one of: Low, Medium, High, Critical.");
        builder.AppendLine("risk must be one of: Low, Medium, High.");
        builder.AppendLine();
        builder.AppendLine($"Ticket Subject: {ticket.Subject}");
        builder.AppendLine($"Ticket Summary: {ticket.Summary}");
        builder.AppendLine("Recent Messages:");

        foreach (TicketMessage message in messages.OrderBy(item => item.CreatedUtc))
        {
            builder.AppendLine($"- {message.AuthorType}: {message.Body}");
        }

        if (articles.Count > 0)
        {
            builder.AppendLine("Knowledge Context:");
            foreach (KnowledgeArticle article in articles)
            {
                builder.AppendLine($"- {article.Title}: {article.ContentMarkdown}");
            }
        }

        return builder.ToString();
    }

    private static string ComputeSha256(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Math.Max(1, text.Length / 4);
    }

    private static AiJsonPayload? ParsePayload(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        int start = raw.IndexOf('{');
        int end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        string json = raw[start..(end + 1)];

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            string category = root.TryGetProperty("category", out JsonElement categoryEl)
                ? categoryEl.GetString() ?? "GeneralRequest"
                : "GeneralRequest";

            string priority = root.TryGetProperty("priority", out JsonElement priorityEl)
                ? priorityEl.GetString() ?? "Medium"
                : "Medium";

            string response = root.TryGetProperty("response", out JsonElement responseEl)
                ? responseEl.GetString() ?? "We are reviewing your request."
                : "We are reviewing your request.";

            string risk = root.TryGetProperty("risk", out JsonElement riskEl)
                ? riskEl.GetString() ?? "Low"
                : "Low";

            double confidence = root.TryGetProperty("confidence", out JsonElement confEl) && confEl.TryGetDouble(out double parsed)
                ? Math.Clamp(parsed, 0, 1)
                : 0.75;

            return new AiJsonPayload(NormalizeCategory(category), NormalizePriority(priority), response.Trim(), NormalizeRisk(risk), confidence);
        }
        catch
        {
            return null;
        }
    }

    private static AiGeneration BuildFallbackGeneration(Ticket ticket, IReadOnlyList<TicketMessage> messages, string promptHash)
    {
        AiJsonPayload payload = BuildFallbackPayload(ticket, messages);

        return new AiGeneration(
            payload.Category,
            payload.Priority,
            payload.Response,
            payload.Risk,
            payload.Confidence,
            EstimateTokens(ticket.Summary),
            EstimateTokens(payload.Response),
            promptHash);
    }

    private static AiJsonPayload BuildFallbackPayload(Ticket ticket, IReadOnlyList<TicketMessage> messages)
    {
        string text = string.Join(" ", messages.Select(item => item.Body)).ToLowerInvariant();

        string category = "GeneralRequest";
        string priority = "Medium";
        string risk = "Low";
        double confidence = 0.78;

        if (text.Contains("password") || text.Contains("login") || text.Contains("mfa"))
        {
            category = "Access";
            priority = "Medium";
        }

        if (text.Contains("down") || text.Contains("outage") || text.Contains("offline"))
        {
            category = "ServiceIncident";
            priority = "High";
            confidence = 0.83;
        }

        if (text.Contains("billing") || text.Contains("invoice"))
        {
            category = "BillingDispute";
            priority = "High";
            risk = "High";
            confidence = 0.66;
        }

        if (text.Contains("security") || text.Contains("breach") || text.Contains("phish") || text.Contains("legal"))
        {
            category = text.Contains("legal") ? "LegalRequest" : "SecurityIncident";
            priority = "Critical";
            risk = "High";
            confidence = 0.61;
        }

        string response = category switch
        {
            "Access" => "Thanks for the details. Please confirm the impacted username and any recent password reset attempts so we can continue.",
            "ServiceIncident" => "We have identified this as a potential service incident and started triage. We will update you with mitigation steps shortly.",
            "BillingDispute" => "We have routed this to billing review. Please share the invoice number and the specific line items in question.",
            "SecurityIncident" => "For security handling, isolate affected systems and avoid further changes. Our team is escalating this incident immediately.",
            "LegalRequest" => "This request requires legal review. We have escalated internally and will follow up through approved channels.",
            _ => "Thank you for contacting support. We are reviewing your ticket and will provide next steps soon."
        };

        return new AiJsonPayload(
            NormalizeCategory(category),
            NormalizePriority(priority),
            response,
            NormalizeRisk(risk),
            Math.Clamp(confidence, 0, 1));
    }

    private static string NormalizeCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return "GeneralRequest";
        }

        return category.Trim().Replace(" ", string.Empty);
    }

    private static string NormalizePriority(string priority)
    {
        return priority.Trim().ToLowerInvariant() switch
        {
            "low" => "Low",
            "high" => "High",
            "critical" => "Critical",
            _ => "Medium"
        };
    }

    private static string NormalizeRisk(string risk)
    {
        return risk.Trim().ToLowerInvariant() switch
        {
            "high" => "High",
            "medium" => "Medium",
            _ => "Low"
        };
    }

    private void AddAudit(Guid? customerId, Guid? actorUserId, string eventType, object payload)
    {
        string payloadJson = JsonSerializer.Serialize(payload);
        dbContext.AuditEvents.Add(new AuditEvent(Guid.NewGuid(), customerId, actorUserId, eventType, payloadJson, DateTime.UtcNow));
    }

    private static AiRunResult ToResult(Guid ticketId, TicketAiSuggestion suggestion, bool autoResponseSent)
    {
        return new AiRunResult(
            ticketId,
            suggestion.SuggestedCategory,
            suggestion.SuggestedPriority,
            suggestion.DraftResponse,
            suggestion.RiskLevel,
            suggestion.Confidence,
            suggestion.Status,
            autoResponseSent);
    }

    private sealed record AiGeneration(
        string SuggestedCategory,
        string SuggestedPriority,
        string DraftResponse,
        string RiskLevel,
        double Confidence,
        int InputTokens,
        int OutputTokens,
        string PromptHash);

    private sealed record AiJsonPayload(string Category, string Priority, string Response, string Risk, double Confidence);
}
