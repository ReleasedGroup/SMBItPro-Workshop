using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Errors;
using Helpdesk.Light.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Light.Api.Controllers;

[ApiController]
[Route("api/v1/ticket-categories")]
[Authorize(Roles = $"{RoleNames.Technician},{RoleNames.MspAdmin}")]
public sealed class TicketCategoriesController(ITicketCategoryAdministrationService ticketCategoryAdministrationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TicketCategorySummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<TicketCategorySummaryDto>>> List([FromQuery] Guid? customerId, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<TicketCategorySummaryDto> categories = await ticketCategoryAdministrationService.ListTicketCategoriesAsync(customerId, cancellationToken);
            return Ok(categories);
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ProducesResponseType<TicketCategorySummaryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketCategorySummaryDto>> Create([FromBody] CreateTicketCategoryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            TicketCategorySummaryDto created = await ticketCategoryAdministrationService.CreateTicketCategoryAsync(request, cancellationToken);
            return Created($"/api/v1/ticket-categories/{created.Id}", created);
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

    [HttpPut("{ticketCategoryId:guid}")]
    [ProducesResponseType<TicketCategorySummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketCategorySummaryDto>> Update(
        Guid ticketCategoryId,
        [FromBody] UpdateTicketCategoryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            TicketCategorySummaryDto updated = await ticketCategoryAdministrationService.UpdateTicketCategoryAsync(ticketCategoryId, request, cancellationToken);
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

    [HttpDelete("{ticketCategoryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid ticketCategoryId, CancellationToken cancellationToken)
    {
        try
        {
            await ticketCategoryAdministrationService.DeleteTicketCategoryAsync(ticketCategoryId, cancellationToken);
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
    }
}
