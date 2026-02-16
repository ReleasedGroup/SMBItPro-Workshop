using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Abstractions.Ai;
using Helpdesk.Light.Application.Contracts.Ai;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Light.Api.Controllers;

[ApiController]
[Route("api/v1/admin/customers")]
[Authorize(Roles = RoleNames.MspAdmin)]
public sealed class AdminCustomersController(
    ICustomerAdministrationService customerService,
    ICustomerAiPolicyService customerAiPolicyService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CustomerSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerSummaryDto>>> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<CustomerSummaryDto> customers = await customerService.ListCustomersAsync(cancellationToken);
        return Ok(customers);
    }

    [HttpPost]
    [ProducesResponseType<CustomerSummaryDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomerSummaryDto>> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        CustomerSummaryDto created = await customerService.CreateCustomerAsync(request, cancellationToken);
        return Created($"/api/v1/customers/{created.Id}", created);
    }

    [HttpPut("{customerId:guid}")]
    [ProducesResponseType<CustomerSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerSummaryDto>> Update(Guid customerId, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            CustomerSummaryDto updated = await customerService.UpdateCustomerAsync(customerId, request, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{customerId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(Guid customerId, CancellationToken cancellationToken)
    {
        try
        {
            await customerService.DeleteCustomerAsync(customerId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPost("{customerId:guid}/domains")]
    [ProducesResponseType<CustomerDomainDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDomainDto>> AddDomain(Guid customerId, [FromBody] AddCustomerDomainRequest request, CancellationToken cancellationToken)
    {
        try
        {
            CustomerDomainDto created = await customerService.AddDomainAsync(customerId, request, cancellationToken);
            return Created($"/api/v1/customers/{customerId}/domains/{created.Id}", created);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpGet("{customerId:guid}/end-users")]
    [ProducesResponseType<IReadOnlyList<EndUserSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EndUserSummaryDto>>> ListEndUsers(Guid customerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<EndUserSummaryDto> users = await customerService.ListEndUsersAsync(customerId, cancellationToken);
        return Ok(users);
    }

    [HttpPost("{customerId:guid}/end-users")]
    [ProducesResponseType<EndUserSummaryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EndUserSummaryDto>> CreateEndUser(Guid customerId, [FromBody] CreateEndUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            EndUserSummaryDto created = await customerService.CreateEndUserAsync(customerId, request, cancellationToken);
            return Created($"/api/v1/admin/customers/{customerId}/end-users/{created.Id}", created);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPut("{customerId:guid}/end-users/{userId:guid}")]
    [ProducesResponseType<EndUserSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EndUserSummaryDto>> UpdateEndUser(Guid customerId, Guid userId, [FromBody] UpdateEndUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            EndUserSummaryDto updated = await customerService.UpdateEndUserAsync(customerId, userId, request, cancellationToken);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpDelete("{customerId:guid}/end-users/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteEndUser(Guid customerId, Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await customerService.DeleteEndUserAsync(customerId, userId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPatch("{customerId:guid}/ai-policy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateAiPolicy(Guid customerId, [FromBody] CustomerAiPolicyUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await customerAiPolicyService.UpdatePolicyAsync(customerId, request, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
