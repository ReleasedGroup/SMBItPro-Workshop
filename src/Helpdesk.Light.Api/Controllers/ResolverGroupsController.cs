using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Errors;
using Helpdesk.Light.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Light.Api.Controllers;

[ApiController]
[Route("api/v1/resolver-groups")]
[Authorize(Roles = $"{RoleNames.Technician},{RoleNames.MspAdmin}")]
public sealed class ResolverGroupsController(IResolverAdministrationService resolverAdministrationService) : ControllerBase
{
    [HttpGet("options")]
    [ProducesResponseType<ResolverAssignmentOptionsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ResolverAssignmentOptionsDto>> GetOptions([FromQuery] Guid customerId, CancellationToken cancellationToken)
    {
        try
        {
            ResolverAssignmentOptionsDto options = await resolverAdministrationService.GetAssignmentOptionsAsync(customerId, cancellationToken);
            return Ok(options);
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ResolverGroupSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ResolverGroupSummaryDto>>> List([FromQuery] Guid? customerId, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<ResolverGroupSummaryDto> groups = await resolverAdministrationService.ListResolverGroupsAsync(customerId, cancellationToken);
            return Ok(groups);
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ProducesResponseType<ResolverGroupSummaryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResolverGroupSummaryDto>> Create([FromBody] CreateResolverGroupRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ResolverGroupSummaryDto created = await resolverAdministrationService.CreateResolverGroupAsync(request, cancellationToken);
            return Created($"/api/v1/resolver-groups/{created.Id}", created);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPut("{resolverGroupId:guid}")]
    [ProducesResponseType<ResolverGroupSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResolverGroupSummaryDto>> Update(
        Guid resolverGroupId,
        [FromBody] UpdateResolverGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            ResolverGroupSummaryDto updated = await resolverAdministrationService.UpdateResolverGroupAsync(resolverGroupId, request, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpDelete("{resolverGroupId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(Guid resolverGroupId, CancellationToken cancellationToken)
    {
        try
        {
            await resolverAdministrationService.DeleteResolverGroupAsync(resolverGroupId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }
}
