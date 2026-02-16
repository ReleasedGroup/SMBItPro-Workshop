namespace Helpdesk.Light.Application.Contracts;

public sealed record ResolverUserSummaryDto(
    Guid Id,
    Guid? CustomerId,
    string Email,
    string DisplayName,
    string Role);

public sealed record ResolverGroupSummaryDto(
    Guid Id,
    Guid CustomerId,
    string Name,
    bool IsActive,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record CreateResolverGroupRequest(Guid CustomerId, string Name, bool IsActive);

public sealed record UpdateResolverGroupRequest(string Name, bool IsActive);

public sealed record ResolverAssignmentOptionsDto(
    IReadOnlyList<ResolverUserSummaryDto> Users,
    IReadOnlyList<ResolverGroupSummaryDto> Groups);
