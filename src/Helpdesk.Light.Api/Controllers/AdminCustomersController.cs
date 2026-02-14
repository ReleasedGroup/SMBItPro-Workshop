using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Light.Api.Controllers;

[ApiController]
[Route("api/v1/admin/customers")]
[Authorize(Roles = RoleNames.MspAdmin)]
public sealed class AdminCustomersController(ICustomerAdministrationService customerService) : ControllerBase
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
}
