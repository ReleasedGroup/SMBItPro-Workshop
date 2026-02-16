using System.Text.Json;
using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Abstractions.Ai;
using Helpdesk.Light.Application.Contracts.Ai;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Errors;
using Helpdesk.Light.Domain.Ai;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly HelpdeskDbContext dbContext;
    private readonly ITenantContextAccessor tenantContextAccessor;
    private readonly IPlatformSettingsService platformSettingsService;
    private readonly ILogger<KnowledgeBaseService> logger;

    public KnowledgeBaseService(
        HelpdeskDbContext dbContext,
        ITenantContextAccessor tenantContextAccessor,
        IPlatformSettingsService platformSettingsService,
        ILogger<KnowledgeBaseService> logger)
    {
        this.dbContext = dbContext;
        this.tenantContextAccessor = tenantContextAccessor;
        this.platformSettingsService = platformSettingsService;
        this.logger = logger;
    }

    public async Task<IReadOnlyList<KnowledgeArticleSummaryDto>> ListAsync(KnowledgeArticleListRequest request, CancellationToken cancellationToken = default)
    {
        TenantAccessContext context = RequireAuthenticatedContext();

        IQueryable<KnowledgeArticle> query = dbContext.KnowledgeArticles.AsNoTracking();
        query = ApplyScope(query, context);

        if (request.CustomerId.HasValue)
        {
            EnsureCustomerScope(context, request.CustomerId.Value);
            query = query.Where(item => item.CustomerId == request.CustomerId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(item => item.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string search = EscapeLikePattern(request.Search.Trim());
            string pattern = $"%{search}%";
            query = query.Where(item =>
                EF.Functions.Like(item.Title, pattern, "\\") ||
                EF.Functions.Like(item.ContentMarkdown, pattern, "\\"));
        }

        int take = Math.Clamp(request.Take, 1, 300);
        return await query
            .OrderByDescending(item => item.UpdatedUtc)
            .Take(take)
            .Select(item => ToSummary(item))
            .ToListAsync(cancellationToken);
    }

    public async Task<KnowledgeArticleDetailDto?> GetAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        KnowledgeArticle? article = await dbContext.KnowledgeArticles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == articleId, cancellationToken);

        if (article is null)
        {
            return null;
        }

        EnsureArticleScope(RequireAuthenticatedContext(), article);
        return ToDetail(article);
    }

    public async Task<KnowledgeArticleDetailDto> CreateDraftAsync(KnowledgeArticleDraftCreateRequest request, CancellationToken cancellationToken = default)
    {
        TenantAccessContext context = RequireTechnicianOrAdminContext();
        DateTime utcNow = DateTime.UtcNow;
        Guid customerId = ResolveCustomerId(request.CustomerId, context);

        Guid? sourceTicketId = request.SourceTicketId;
        if (sourceTicketId.HasValue)
        {
            Ticket? source = await dbContext.Tickets
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == sourceTicketId.Value, cancellationToken);

            if (source is null)
            {
                throw new KeyNotFoundException($"Ticket '{sourceTicketId}' was not found.");
            }

            if (source.CustomerId != customerId)
            {
                throw new TenantAccessDeniedException("Cannot link article to a ticket outside tenant boundary.");
            }
        }

        KnowledgeArticle article = new(
            Guid.NewGuid(),
            customerId,
            sourceTicketId,
            request.Title,
            request.ContentMarkdown,
            request.AiGenerated,
            context.UserId,
            utcNow);

        dbContext.KnowledgeArticles.Add(article);
        AddAuditEvent(customerId, context.UserId, "knowledge.article.created", new { articleId = article.Id, article.Title, article.Status, article.AiGenerated });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDetail(article);
    }

    public async Task<KnowledgeArticleDetailDto> UpdateDraftAsync(Guid articleId, KnowledgeArticleDraftUpdateRequest request, CancellationToken cancellationToken = default)
    {
        TenantAccessContext context = RequireTechnicianOrAdminContext();

        KnowledgeArticle article = await dbContext.KnowledgeArticles
            .SingleOrDefaultAsync(item => item.Id == articleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Knowledge article '{articleId}' was not found.");

        EnsureArticleScope(context, article);
        article.UpdateDraft(request.Title, request.ContentMarkdown, context.UserId, DateTime.UtcNow);

        AddAuditEvent(article.CustomerId, context.UserId, "knowledge.article.updated", new { articleId = article.Id, article.Version, article.Status });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDetail(article);
    }

    public async Task<KnowledgeArticleDetailDto> GenerateDraftFromTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        TenantAccessContext context = RequireTechnicianOrAdminContext();

        Ticket ticket = await dbContext.Tickets
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket '{ticketId}' was not found.");

        EnsureCustomerScope(context, ticket.CustomerId);
        if (ticket.Status is not (TicketStatus.Resolved or TicketStatus.Closed))
        {
            throw new InvalidOperationException("Knowledge article drafts can only be generated from resolved or closed tickets.");
        }

        List<TicketMessage> messages = await dbContext.TicketMessages
            .AsNoTracking()
            .Where(item => item.TicketId == ticketId)
            .OrderBy(item => item.CreatedUtc)
            .ToListAsync(cancellationToken);

        GeneratedArticleDraft draft = await GenerateArticleDraftAsync(ticket, messages, cancellationToken);

        KnowledgeArticle article = new(
            Guid.NewGuid(),
            ticket.CustomerId,
            ticket.Id,
            draft.Title,
            draft.ContentMarkdown,
            aiGenerated: true,
            context.UserId,
            DateTime.UtcNow);

        dbContext.KnowledgeArticles.Add(article);
        AddAuditEvent(ticket.CustomerId, context.UserId, "knowledge.article.generated_from_ticket", new
        {
            articleId = article.Id,
            sourceTicketId = ticket.Id,
            usedAiModel = draft.UsedAiModel
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDetail(article);
    }

    public async Task<KnowledgeArticleDetailDto> PublishAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        TenantAccessContext context = RequireTechnicianOrAdminContext();

        KnowledgeArticle article = await dbContext.KnowledgeArticles
            .SingleOrDefaultAsync(item => item.Id == articleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Knowledge article '{articleId}' was not found.");

        EnsureArticleScope(context, article);
        article.Publish(context.UserId, DateTime.UtcNow);
        AddAuditEvent(article.CustomerId, context.UserId, "knowledge.article.published", new { articleId = article.Id, article.Version });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDetail(article);
    }

    public async Task<KnowledgeArticleDetailDto> ArchiveAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        TenantAccessContext context = RequireTechnicianOrAdminContext();

        KnowledgeArticle article = await dbContext.KnowledgeArticles
            .SingleOrDefaultAsync(item => item.Id == articleId, cancellationToken)
            ?? throw new KeyNotFoundException($"Knowledge article '{articleId}' was not found.");

        EnsureArticleScope(context, article);
        article.Archive(context.UserId, DateTime.UtcNow);
        AddAuditEvent(article.CustomerId, context.UserId, "knowledge.article.archived", new { articleId = article.Id, article.Version });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDetail(article);
    }

    private static Kernel? BuildKernel(RuntimePlatformSettings settings)
    {
        if (!settings.EnableAi || string.IsNullOrWhiteSpace(settings.OpenAIApiKey))
        {
            return null;
        }

        IKernelBuilder builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(settings.ModelId, settings.OpenAIApiKey);
        return builder.Build();
    }

    private async Task<GeneratedArticleDraft> GenerateArticleDraftAsync(
        Ticket ticket,
        IReadOnlyList<TicketMessage> messages,
        CancellationToken cancellationToken)
    {
        string fallbackTitle = $"Runbook: {ticket.Subject}";
        string fallbackMarkdown = BuildFallbackArticleMarkdown(ticket, messages);
        RuntimePlatformSettings runtimeSettings = await platformSettingsService.GetRuntimeSettingsAsync(cancellationToken);
        Kernel? kernel = BuildKernel(runtimeSettings);

        if (kernel is null)
        {
            return new GeneratedArticleDraft(fallbackTitle, fallbackMarkdown, false);
        }

        string prompt = BuildKnowledgePrompt(ticket, messages);
        try
        {
            FunctionResult result = await kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
            string? raw = result.GetValue<string>();
            GeneratedArticlePayload? payload = ParseGeneratedPayload(raw);

            if (payload is null || string.IsNullOrWhiteSpace(payload.Title) || string.IsNullOrWhiteSpace(payload.ContentMarkdown))
            {
                return new GeneratedArticleDraft(fallbackTitle, fallbackMarkdown, false);
            }

            return new GeneratedArticleDraft(payload.Title.Trim(), payload.ContentMarkdown.Trim(), true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to generate knowledge article draft using AI; using fallback.");
            return new GeneratedArticleDraft(fallbackTitle, fallbackMarkdown, false);
        }
    }

    private static string BuildKnowledgePrompt(Ticket ticket, IReadOnlyList<TicketMessage> messages)
    {
        string timeline = string.Join("\n", messages.Select(item => $"- [{item.CreatedUtc:O}] {item.AuthorType}: {item.Body}"));
        return $$"""
                 You are creating an MSP helpdesk knowledge article from a resolved ticket.
                 Return JSON only with keys: title, contentMarkdown.
                 Requirements:
                 - Make title concise and actionable.
                 - contentMarkdown must include sections: Summary, Symptoms, Cause, Resolution, Validation, Prevention.
                 - Use operational tone suitable for technicians.
                 - Do not include personal data.

                 Ticket reference: {{ticket.ReferenceCode}}
                 Subject: {{ticket.Subject}}
                 Category: {{ticket.Category}}
                 Priority: {{ticket.Priority}}
                 Conversation timeline:
                 {{timeline}}
                 """;
    }

    private static GeneratedArticlePayload? ParseGeneratedPayload(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(raw);
            JsonElement root = document.RootElement;
            string? title = root.TryGetProperty("title", out JsonElement titleElement) ? titleElement.GetString() : null;
            string? content = root.TryGetProperty("contentMarkdown", out JsonElement contentElement) ? contentElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            return new GeneratedArticlePayload(title, content);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildFallbackArticleMarkdown(Ticket ticket, IReadOnlyList<TicketMessage> messages)
    {
        string latestResolution = messages.LastOrDefault()?.Body ?? ticket.Summary;
        return $"""
                ## Summary
                Ticket `{ticket.ReferenceCode}` was resolved for `{ticket.Subject}`.

                ## Symptoms
                - User reported: {ticket.Summary}

                ## Cause
                - Root cause investigation completed by support team.

                ## Resolution
                - {latestResolution}

                ## Validation
                - Confirmed service behavior returned to normal after remediation.

                ## Prevention
                - Capture recurring indicators and update monitoring thresholds for early detection.
                """;
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);
    }

    private static KnowledgeArticleSummaryDto ToSummary(KnowledgeArticle article)
    {
        return new KnowledgeArticleSummaryDto(
            article.Id,
            article.CustomerId,
            article.SourceTicketId,
            article.Title,
            article.Status,
            article.Version,
            article.AiGenerated,
            article.UpdatedUtc);
    }

    private static KnowledgeArticleDetailDto ToDetail(KnowledgeArticle article)
    {
        return new KnowledgeArticleDetailDto(
            article.Id,
            article.CustomerId,
            article.SourceTicketId,
            article.Title,
            article.ContentMarkdown,
            article.Status,
            article.Version,
            article.AiGenerated,
            article.LastEditedByUserId,
            article.PublishedByUserId,
            article.ArchivedByUserId,
            article.CreatedUtc,
            article.UpdatedUtc,
            article.PublishedUtc,
            article.ArchivedUtc);
    }

    private IQueryable<KnowledgeArticle> ApplyScope(IQueryable<KnowledgeArticle> query, TenantAccessContext context)
    {
        if (context.IsMspAdmin)
        {
            return query;
        }

        if (!context.CustomerId.HasValue)
        {
            return query.Where(_ => false);
        }

        return query.Where(item => item.CustomerId == context.CustomerId.Value);
    }

    private static void EnsureCustomerScope(TenantAccessContext context, Guid customerId)
    {
        if (context.IsMspAdmin)
        {
            return;
        }

        if (!context.CustomerId.HasValue || context.CustomerId.Value != customerId)
        {
            throw new TenantAccessDeniedException("Knowledge article operation crossed tenant boundary.");
        }
    }

    private static void EnsureArticleScope(TenantAccessContext context, KnowledgeArticle article)
    {
        if (article.CustomerId.HasValue)
        {
            EnsureCustomerScope(context, article.CustomerId.Value);
            return;
        }

        if (!context.IsMspAdmin)
        {
            throw new TenantAccessDeniedException("Global knowledge articles are restricted to MSP admin.");
        }
    }

    private static Guid ResolveCustomerId(Guid? requestedCustomerId, TenantAccessContext context)
    {
        if (context.IsMspAdmin)
        {
            if (!requestedCustomerId.HasValue || requestedCustomerId.Value == Guid.Empty)
            {
                throw new InvalidOperationException("Admin draft creation requires customer id.");
            }

            return requestedCustomerId.Value;
        }

        if (!context.CustomerId.HasValue)
        {
            throw new UnauthorizedAccessException("Authenticated user is missing tenant context.");
        }

        if (requestedCustomerId.HasValue && requestedCustomerId.Value != context.CustomerId.Value)
        {
            throw new TenantAccessDeniedException("Cannot create knowledge article for another tenant.");
        }

        return context.CustomerId.Value;
    }

    private TenantAccessContext RequireAuthenticatedContext()
    {
        TenantAccessContext context = tenantContextAccessor.Current;
        if (!context.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        return context;
    }

    private TenantAccessContext RequireTechnicianOrAdminContext()
    {
        TenantAccessContext context = RequireAuthenticatedContext();
        if (!context.IsMspAdmin && !string.Equals(context.Role, RoleNames.Technician, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Technician or admin role is required.");
        }

        return context;
    }

    private void AddAuditEvent(Guid? customerId, Guid? actorUserId, string eventType, object payload)
    {
        dbContext.AuditEvents.Add(new AuditEvent(
            Guid.NewGuid(),
            customerId,
            actorUserId,
            eventType,
            JsonSerializer.Serialize(payload),
            DateTime.UtcNow));
    }

    private sealed record GeneratedArticleDraft(string Title, string ContentMarkdown, bool UsedAiModel);

    private sealed record GeneratedArticlePayload(string Title, string ContentMarkdown);
}
