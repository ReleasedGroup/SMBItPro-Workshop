using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Domain.Email;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Light.Api.Controllers;

[ApiController]
[Route("api/v1/ops")]
[Authorize(Roles = RoleNames.MspAdmin)]
public sealed class OperationsController(
    IRuntimeMetricsRecorder runtimeMetricsRecorder,
    HelpdeskDbContext dbContext) : ControllerBase
{
    [HttpGet("metrics")]
    [ProducesResponseType<OperationsMetricsDto>(StatusCodes.Status200OK)]
    public ActionResult<OperationsMetricsDto> GetMetrics()
    {
        return Ok(runtimeMetricsRecorder.GetSnapshot());
    }

    [HttpGet("dead-letters")]
    [ProducesResponseType<IReadOnlyList<DeadLetterMessageDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DeadLetterMessageDto>>> ListDeadLetters([FromQuery] int take, CancellationToken cancellationToken)
    {
        int boundedTake = take <= 0 ? 50 : Math.Clamp(take, 1, 500);

        List<DeadLetterMessageDto> messages = await dbContext.OutboundEmailMessages
            .AsNoTracking()
            .Where(item => item.Status == OutboundEmailStatus.DeadLetter)
            .OrderByDescending(item => item.DeadLetteredUtc ?? item.CreatedUtc)
            .Take(boundedTake)
            .Select(item => new DeadLetterMessageDto(
                item.Id,
                item.TicketId,
                item.CustomerId,
                item.ToAddress,
                item.Subject,
                item.AttemptCount,
                item.LastError,
                item.CreatedUtc,
                item.DeadLetteredUtc))
            .ToListAsync(cancellationToken);

        return Ok(messages);
    }
}
