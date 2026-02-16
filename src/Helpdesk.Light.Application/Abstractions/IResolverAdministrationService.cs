using Helpdesk.Light.Application.Contracts;

namespace Helpdesk.Light.Application.Abstractions;

public interface IResolverAdministrationService
{
    Task<ResolverAssignmentOptionsDto> GetAssignmentOptionsAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResolverGroupSummaryDto>> ListResolverGroupsAsync(Guid? customerId, CancellationToken cancellationToken = default);

    Task<ResolverGroupSummaryDto> CreateResolverGroupAsync(CreateResolverGroupRequest request, CancellationToken cancellationToken = default);

    Task<ResolverGroupSummaryDto> UpdateResolverGroupAsync(Guid resolverGroupId, UpdateResolverGroupRequest request, CancellationToken cancellationToken = default);

    Task DeleteResolverGroupAsync(Guid resolverGroupId, CancellationToken cancellationToken = default);
}
