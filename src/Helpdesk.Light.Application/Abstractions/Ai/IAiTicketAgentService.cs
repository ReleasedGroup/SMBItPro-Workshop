using Helpdesk.Light.Application.Contracts.Ai;

namespace Helpdesk.Light.Application.Abstractions.Ai;

public interface IAiTicketAgentService
{
    Task<AiRunResult> RunForTicketAsync(AiRunRequest request, CancellationToken cancellationToken = default);

    Task<AiRunResult?> ApproveSuggestionAsync(Guid ticketId, TicketAiApprovalRequest request, CancellationToken cancellationToken = default);

    Task<AiRunResult?> DiscardSuggestionAsync(Guid ticketId, CancellationToken cancellationToken = default);
}
