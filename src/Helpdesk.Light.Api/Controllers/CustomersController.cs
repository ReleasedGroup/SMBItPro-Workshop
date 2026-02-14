using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.Light.Api.Controllers;

[ApiController]
[Route("api/v1/customers")]
[Authorize]
public sealed class CustomersController(ICustomerAdministrationService customerService, ITenantContextAccessor tenantContextAccessor) : ControllerBase
{
    [HttpGet("current")]
    [ProducesResponseType<CustomerDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerDetailDto>> Current(CancellationToken cancellationToken)
    {
        TenantAccessContext context = tenantContextAccessor.Current;
        if (!context.CustomerId.HasValue)
        {
            return BadRequest(new { message = "Authenticated user is not bound to a customer tenant." });
        }

        CustomerDetailDto? customer = await customerService.GetCustomerAsync(context.CustomerId.Value, cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        return Ok(customer);
    }

    [HttpGet("{customerId:guid}")]
    [ProducesResponseType<CustomerDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDetailDto>> Get(Guid customerId, CancellationToken cancellationToken)
    {
        try
        {
            CustomerDetailDto? customer = await customerService.GetCustomerAsync(customerId, cancellationToken);
            if (customer is null)
            {
                return NotFound();
            }

            return Ok(customer);
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
    }

    [HttpGet("{customerId:guid}/domains")]
    [ProducesResponseType<IReadOnlyList<CustomerDomainDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CustomerDomainDto>>> ListDomains(Guid customerId, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<CustomerDomainDto> domains = await customerService.ListCustomerDomainsAsync(customerId, cancellationToken);
            return Ok(domains);
        }
        catch (TenantAccessDeniedException)
        {
            return Forbid();
        }
    }
}
