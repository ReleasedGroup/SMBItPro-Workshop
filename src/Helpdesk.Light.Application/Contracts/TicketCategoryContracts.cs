namespace Helpdesk.Light.Application.Contracts;

public sealed record TicketCategorySummaryDto(
    Guid Id,
    Guid CustomerId,
    string Name,
    bool IsActive,
    Guid? ResolverGroupId,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record CreateTicketCategoryRequest(
    Guid CustomerId,
    string Name,
    bool IsActive,
    Guid? ResolverGroupId);

public sealed record UpdateTicketCategoryRequest(
    string Name,
    bool IsActive,
    Guid? ResolverGroupId);
