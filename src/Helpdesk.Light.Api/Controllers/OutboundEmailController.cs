using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Abstractions.Email;
using Helpdesk.Light.Application.Contracts.Email;
using Helpdesk.Light.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Light.Api.Controllers;

[ApiController]
[Route("api/v1/email/outbound")]
[Authorize(Roles = RoleNames.MspAdmin)]
public sealed class OutboundEmailController(
    IOutboundEmailService outboundEmailService,
    ITenantContextAccessor tenantContextAccessor) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OutboundEmailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OutboundEmailDto>>> List([FromQuery] Guid? customerId, CancellationToken cancellationToken)
    {
        TenantAccessContext context = tenantContextAccessor.Current;
        Guid? scopedCustomerId = context.IsMspAdmin ? customerId : context.CustomerId;

        IReadOnlyList<OutboundEmailDto> messages = await outboundEmailService.ListAsync(scopedCustomerId, cancellationToken);
        return Ok(messages);
    }

    [HttpPost("dispatch")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Dispatch(CancellationToken cancellationToken)
    {
        await outboundEmailService.DispatchPendingAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("retry-dead-letter")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> RetryDeadLetter([FromQuery] int take, CancellationToken cancellationToken)
    {
        int retried = await outboundEmailService.RetryDeadLettersAsync(take <= 0 ? 50 : take, cancellationToken);
        return Ok(new { retried });
    }
}
