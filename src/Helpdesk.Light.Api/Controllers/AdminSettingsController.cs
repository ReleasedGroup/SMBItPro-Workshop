using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Errors;
using Helpdesk.Light.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Light.Api.Controllers;

[ApiController]
[Route("api/v1/admin/settings")]
[Authorize(Roles = RoleNames.MspAdmin)]
public sealed class AdminSettingsController(IPlatformSettingsService platformSettingsService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PlatformSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PlatformSettingsDto>> Get(CancellationToken cancellationToken)
    {
        try
        {
            PlatformSettingsDto settings = await platformSettingsService.GetAdminSettingsAsync(cancellationToken);
            return Ok(settings);
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpPut]
    [ProducesResponseType<PlatformSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PlatformSettingsDto>> Update([FromBody] PlatformSettingsUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            PlatformSettingsDto updated = await platformSettingsService.UpdateAdminSettingsAsync(request, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
    }
}
