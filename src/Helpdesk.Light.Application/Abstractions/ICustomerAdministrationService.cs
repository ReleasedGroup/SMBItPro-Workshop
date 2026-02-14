using Helpdesk.Light.Application.Contracts;

namespace Helpdesk.Light.Application.Abstractions;

public interface ICustomerAdministrationService
{
    Task<IReadOnlyList<CustomerSummaryDto>> ListCustomersAsync(CancellationToken cancellationToken = default);

    Task<CustomerSummaryDto> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<CustomerDomainDto> AddDomainAsync(Guid customerId, AddCustomerDomainRequest request, CancellationToken cancellationToken = default);

    Task<CustomerDetailDto?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerDomainDto>> ListCustomerDomainsAsync(Guid customerId, CancellationToken cancellationToken = default);
}
