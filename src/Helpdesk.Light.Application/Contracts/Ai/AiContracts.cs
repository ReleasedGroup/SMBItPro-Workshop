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
