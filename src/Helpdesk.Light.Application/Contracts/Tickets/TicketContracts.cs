using Helpdesk.Light.Domain.Ai;
using Helpdesk.Light.Domain.Tickets;

namespace Helpdesk.Light.Application.Contracts.Tickets;

public sealed record CreateTicketRequest(
    Guid? CustomerId,
    string Subject,
    string Description,
    TicketPriority Priority,
    string? EndUserEmail = null);

public sealed record TicketMessageCreateRequest(string Body);

public sealed record TicketAssignRequest(Guid? AssignedToUserId, Guid? ResolverGroupId = null);

public sealed record TicketStatusUpdateRequest(TicketStatus Status);

public sealed record TicketTriageUpdateRequest(TicketPriority Priority, string? Category);

public sealed record TicketFilterRequest(TicketStatus? Status, TicketPriority? Priority, Guid? CustomerId, Guid? AssignedToUserId, int Take = 100);

public sealed record TicketSummaryDto(
    Guid Id,
    string ReferenceCode,
    Guid CustomerId,
    Guid CreatedByUserId,
    TicketChannel Channel,
    TicketStatus Status,
    TicketPriority Priority,
    string? Category,
    string Subject,
    string Summary,
    Guid? AssignedToUserId,
    Guid? ResolverGroupId,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    DateTime? ResolvedUtc,
    string? LatestAiSuggestionStatus,
    double? LatestAiConfidence);

public sealed record TicketMessageDto(
    Guid Id,
    Guid TicketId,
    TicketAuthorType AuthorType,
    Guid? AuthorUserId,
    string Body,
    TicketMessageSource Source,
    string? ExternalMessageId,
    DateTime CreatedUtc);

public sealed record TicketAttachmentDto(
    Guid Id,
    Guid TicketId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedUtc);

public sealed record TicketDetailDto(
    TicketSummaryDto Ticket,
    IReadOnlyList<TicketMessageDto> Messages,
    IReadOnlyList<TicketAttachmentDto> Attachments,
    TicketAiSuggestionDto? LatestAiSuggestion);

public sealed record TicketAiSuggestionDto(
    Guid Id,
    Guid TicketId,
    string DraftResponse,
    string SuggestedCategory,
    string SuggestedPriority,
    string RiskLevel,
    double Confidence,
    AiSuggestionStatus Status,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    Guid? ProcessedByUserId);
