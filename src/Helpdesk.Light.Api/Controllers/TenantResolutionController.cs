using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Light.Api.Controllers;

[ApiController]
[Route("api/v1/tenant-resolution")]
[Authorize(Roles = RoleNames.MspAdmin)]
public sealed class TenantResolutionController(ITenantResolutionService tenantResolutionService) : ControllerBase
{
    [HttpPost("resolve")]
    [ProducesResponseType<TenantResolutionResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantResolutionResultDto>> Resolve([FromBody] ResolveSenderRequest request, CancellationToken cancellationToken)
    {
        TenantResolutionResultDto resolved = await tenantResolutionService.ResolveSenderEmailAsync(request, cancellationToken);
        return Ok(resolved);
    }

    [HttpGet("unmapped")]
    [ProducesResponseType<IReadOnlyList<UnmappedInboundItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UnmappedInboundItemDto>>> ListUnmapped(CancellationToken cancellationToken)
    {
        IReadOnlyList<UnmappedInboundItemDto> queue = await tenantResolutionService.ListUnmappedQueueAsync(cancellationToken);
        return Ok(queue);
    }
}
