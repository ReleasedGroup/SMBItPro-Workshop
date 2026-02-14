using Helpdesk.Light.Application.Abstractions.Email;
using Helpdesk.Light.Application.Contracts.Email;
using Helpdesk.Light.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Light.Api.Controllers;

[ApiController]
[Route("api/v1/email/inbound")]
[Authorize(Roles = RoleNames.MspAdmin)]
public sealed class InboundEmailController(IInboundEmailService inboundEmailService) : ControllerBase
{
    [HttpPost("dev")]
    [ProducesResponseType<InboundEmailProcessResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<InboundEmailProcessResult>> ProcessDevInbound(
        [FromBody] InboundEmailRequest request,
        CancellationToken cancellationToken)
    {
        InboundEmailProcessResult result = await inboundEmailService.ProcessAsync(request, cancellationToken);
        return Ok(result);
    }
}
