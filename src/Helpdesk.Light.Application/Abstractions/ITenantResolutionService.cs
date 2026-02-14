using Helpdesk.Light.Application.Contracts;

namespace Helpdesk.Light.Application.Abstractions;

public interface ITenantResolutionService
{
    Task<TenantResolutionResultDto> ResolveSenderEmailAsync(ResolveSenderRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UnmappedInboundItemDto>> ListUnmappedQueueAsync(CancellationToken cancellationToken = default);
}
