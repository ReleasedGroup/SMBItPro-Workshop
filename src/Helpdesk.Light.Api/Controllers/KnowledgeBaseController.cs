using Helpdesk.Light.Application.Abstractions.Ai;
using Helpdesk.Light.Application.Contracts.Ai;
using Helpdesk.Light.Application.Errors;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Domain.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Light.Api.Controllers;

[ApiController]
[Route("api/v1/knowledge/articles")]
[Authorize(Roles = $"{RoleNames.Technician},{RoleNames.MspAdmin}")]
public sealed class KnowledgeBaseController(IKnowledgeBaseService knowledgeBaseService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<KnowledgeArticleSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<KnowledgeArticleSummaryDto>>> List(
        [FromQuery] string? search,
        [FromQuery] KnowledgeArticleStatus? status,
        [FromQuery] Guid? customerId,
        [FromQuery] int take,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<KnowledgeArticleSummaryDto> articles = await knowledgeBaseService.ListAsync(
            new KnowledgeArticleListRequest(search, status, customerId, take <= 0 ? 100 : take),
            cancellationToken);

        return Ok(articles);
    }

    [HttpGet("{articleId:guid}")]
    [ProducesResponseType<KnowledgeArticleDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KnowledgeArticleDetailDto>> Get(Guid articleId, CancellationToken cancellationToken)
    {
        try
        {
            KnowledgeArticleDetailDto? article = await knowledgeBaseService.GetAsync(articleId, cancellationToken);
            return article is null ? NotFound() : Ok(article);
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ProducesResponseType<KnowledgeArticleDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<KnowledgeArticleDetailDto>> CreateDraft(
        [FromBody] KnowledgeArticleDraftCreateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            KnowledgeArticleDetailDto created = await knowledgeBaseService.CreateDraftAsync(request, cancellationToken);
            return Created($"/api/v1/knowledge/articles/{created.Id}", created);
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

    [HttpPut("{articleId:guid}")]
    [ProducesResponseType<KnowledgeArticleDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KnowledgeArticleDetailDto>> UpdateDraft(
        Guid articleId,
        [FromBody] KnowledgeArticleDraftUpdateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            KnowledgeArticleDetailDto updated = await knowledgeBaseService.UpdateDraftAsync(articleId, request, cancellationToken);
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

    [HttpPost("from-ticket/{ticketId:guid}")]
    [ProducesResponseType<KnowledgeArticleDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KnowledgeArticleDetailDto>> GenerateFromTicket(Guid ticketId, CancellationToken cancellationToken)
    {
        try
        {
            KnowledgeArticleDetailDto created = await knowledgeBaseService.GenerateDraftFromTicketAsync(ticketId, cancellationToken);
            return Created($"/api/v1/knowledge/articles/{created.Id}", created);
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

    [HttpPost("{articleId:guid}/publish")]
    [ProducesResponseType<KnowledgeArticleDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KnowledgeArticleDetailDto>> Publish(Guid articleId, CancellationToken cancellationToken)
    {
        try
        {
            KnowledgeArticleDetailDto published = await knowledgeBaseService.PublishAsync(articleId, cancellationToken);
            return Ok(published);
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

    [HttpPost("{articleId:guid}/archive")]
    [ProducesResponseType<KnowledgeArticleDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KnowledgeArticleDetailDto>> Archive(Guid articleId, CancellationToken cancellationToken)
    {
        try
        {
            KnowledgeArticleDetailDto archived = await knowledgeBaseService.ArchiveAsync(articleId, cancellationToken);
            return Ok(archived);
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
}
