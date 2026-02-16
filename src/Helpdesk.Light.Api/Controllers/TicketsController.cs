using Helpdesk.Light.Application.Abstractions.Ai;
using Helpdesk.Light.Application.Abstractions.Tickets;
using Helpdesk.Light.Application.Contracts.Ai;
using Helpdesk.Light.Application.Contracts.Tickets;
using Helpdesk.Light.Application.Errors;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Domain.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Light.Api.Controllers;

[ApiController]
[Route("api/v1/tickets")]
[Authorize]
public sealed class TicketsController(
    ITicketService ticketService,
    IAiTicketAgentService aiTicketAgentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TicketSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketSummaryDto>>> List(
        [FromQuery] TicketStatus? status,
        [FromQuery] TicketPriority? priority,
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? assignedToUserId,
        [FromQuery] int take,
        CancellationToken cancellationToken)
    {
        TicketFilterRequest request = new(status, priority, customerId, assignedToUserId, take <= 0 ? 100 : take);
        IReadOnlyList<TicketSummaryDto> tickets = await ticketService.ListTicketsAsync(request, cancellationToken);
        return Ok(tickets);
    }

    [HttpPost]
    [ProducesResponseType<TicketSummaryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TicketSummaryDto>> Create([FromBody] CreateTicketRequest request, CancellationToken cancellationToken)
    {
        try
        {
            TicketSummaryDto created = await ticketService.CreateTicketAsync(request, cancellationToken);
            return Created($"/api/v1/tickets/{created.Id}", created);
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("public")]
    [AllowAnonymous]
    [ProducesResponseType<TicketSummaryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketSummaryDto>> CreatePublic([FromBody] CreateTicketRequest request, CancellationToken cancellationToken)
    {
        try
        {
            TicketSummaryDto created = await ticketService.CreateTicketAsync(request, cancellationToken);
            return Created($"/api/v1/tickets/{created.Id}", created);
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
        catch (UnauthorizedAccessException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("{ticketId:guid}")]
    [ProducesResponseType<TicketDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDetailDto>> Get(Guid ticketId, CancellationToken cancellationToken)
    {
        try
        {
            TicketDetailDto? ticket = await ticketService.GetTicketAsync(ticketId, cancellationToken);
            return ticket is null ? NotFound() : Ok(ticket);
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpPost("{ticketId:guid}/messages")]
    [ProducesResponseType<TicketMessageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketMessageDto>> AddMessage(Guid ticketId, [FromBody] TicketMessageCreateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            TicketMessageDto created = await ticketService.AddMessageAsync(ticketId, request, cancellationToken);
            return Created($"/api/v1/tickets/{ticketId}/messages/{created.Id}", created);
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

    [HttpPost("{ticketId:guid}/assign")]
    [Authorize(Roles = $"{RoleNames.Technician},{RoleNames.MspAdmin}")]
    [ProducesResponseType<TicketSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketSummaryDto>> Assign(Guid ticketId, [FromBody] TicketAssignRequest request, CancellationToken cancellationToken)
    {
        try
        {
            TicketSummaryDto updated = await ticketService.AssignTicketAsync(ticketId, request, cancellationToken);
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
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{ticketId:guid}/status")]
    [Authorize(Roles = $"{RoleNames.Technician},{RoleNames.MspAdmin}")]
    [ProducesResponseType<TicketSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketSummaryDto>> UpdateStatus(Guid ticketId, [FromBody] TicketStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            TicketSummaryDto updated = await ticketService.UpdateStatusAsync(ticketId, request, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpPost("{ticketId:guid}/triage")]
    [Authorize(Roles = $"{RoleNames.Technician},{RoleNames.MspAdmin}")]
    [ProducesResponseType<TicketSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketSummaryDto>> UpdateTriage(Guid ticketId, [FromBody] TicketTriageUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            TicketSummaryDto updated = await ticketService.UpdateTriageAsync(ticketId, request, cancellationToken);
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
    }

    [HttpPost("{ticketId:guid}/attachments")]
    [ProducesResponseType<TicketAttachmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketAttachmentDto>> UploadAttachment(Guid ticketId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Attachment file is required." });
        }

        try
        {
            await using Stream stream = file.OpenReadStream();
            AttachmentUploadRequest request = new(file.FileName, file.ContentType, file.Length, stream);
            TicketAttachmentDto created = await ticketService.UploadAttachmentAsync(ticketId, request, cancellationToken);
            return Created($"/api/v1/tickets/{ticketId}/attachments/{created.Id}", created);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpGet("{ticketId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DownloadAttachment(Guid ticketId, Guid attachmentId, CancellationToken cancellationToken)
    {
        try
        {
            AttachmentDownloadResult? result = await ticketService.DownloadAttachmentAsync(ticketId, attachmentId, cancellationToken);
            if (result is null)
            {
                return NotFound();
            }

            return File(result.Content, result.ContentType, result.FileName);
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

    [HttpPost("{ticketId:guid}/ai/run")]
    [Authorize(Roles = $"{RoleNames.Technician},{RoleNames.MspAdmin}")]
    [ProducesResponseType<AiRunResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AiRunResult>> RunAi(Guid ticketId, CancellationToken cancellationToken)
    {
        try
        {
            AiRunResult result = await aiTicketAgentService.RunForTicketAsync(new AiRunRequest(ticketId, "ManualRun"), cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{ticketId:guid}/ai/approve-response")]
    [Authorize(Roles = $"{RoleNames.Technician},{RoleNames.MspAdmin}")]
    [ProducesResponseType<AiRunResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AiRunResult>> ApproveAiResponse(Guid ticketId, [FromBody] TicketAiApprovalRequest request, CancellationToken cancellationToken)
    {
        try
        {
            AiRunResult? result = await aiTicketAgentService.ApproveSuggestionAsync(ticketId, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
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

    [HttpPost("{ticketId:guid}/ai/discard-response")]
    [Authorize(Roles = $"{RoleNames.Technician},{RoleNames.MspAdmin}")]
    [ProducesResponseType<AiRunResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AiRunResult>> DiscardAiResponse(Guid ticketId, CancellationToken cancellationToken)
    {
        try
        {
            AiRunResult? result = await aiTicketAgentService.DiscardSuggestionAsync(ticketId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
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
