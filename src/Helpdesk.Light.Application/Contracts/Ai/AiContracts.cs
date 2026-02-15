using Helpdesk.Light.Domain.Ai;

namespace Helpdesk.Light.Application.Contracts.Ai;

public sealed record AiRunRequest(Guid TicketId, string Trigger);

public sealed record AiRunResult(
    Guid TicketId,
    string SuggestedCategory,
    string SuggestedPriority,
    string DraftResponse,
    string RiskLevel,
    double Confidence,
    AiSuggestionStatus Status,
    bool AutoResponseSent);

public sealed record TicketAiApprovalRequest(string? EditedResponse);

public sealed record CustomerAiPolicyUpdateRequest(AiPolicyMode Mode, double AutoRespondMinConfidence);

public sealed record KnowledgeArticleListRequest(
    string? Search,
    KnowledgeArticleStatus? Status,
    Guid? CustomerId,
    int Take = 100);

public sealed record KnowledgeArticleDraftCreateRequest(
    Guid? CustomerId,
    Guid? SourceTicketId,
    string Title,
    string ContentMarkdown,
    bool AiGenerated);

public sealed record KnowledgeArticleDraftUpdateRequest(
    string Title,
    string ContentMarkdown);

public sealed record KnowledgeArticleSummaryDto(
    Guid Id,
    Guid? CustomerId,
    Guid? SourceTicketId,
    string Title,
    KnowledgeArticleStatus Status,
    int Version,
    bool AiGenerated,
    DateTime UpdatedUtc);

public sealed record KnowledgeArticleDetailDto(
    Guid Id,
    Guid? CustomerId,
    Guid? SourceTicketId,
    string Title,
    string ContentMarkdown,
    KnowledgeArticleStatus Status,
    int Version,
    bool AiGenerated,
    Guid? LastEditedByUserId,
    Guid? PublishedByUserId,
    Guid? ArchivedByUserId,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    DateTime? PublishedUtc,
    DateTime? ArchivedUtc);
