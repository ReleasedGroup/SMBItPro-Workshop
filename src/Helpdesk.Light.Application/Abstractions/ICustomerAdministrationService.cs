using Helpdesk.Light.Application.Contracts;

namespace Helpdesk.Light.Application.Abstractions;

public interface ICustomerAdministrationService
{
    Task<IReadOnlyList<CustomerSummaryDto>> ListCustomersAsync(CancellationToken cancellationToken = default);

    Task<CustomerSummaryDto> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<CustomerSummaryDto> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default);

    Task DeleteCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<CustomerDomainDto> AddDomainAsync(Guid customerId, AddCustomerDomainRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EndUserSummaryDto>> ListEndUsersAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<EndUserSummaryDto> CreateEndUserAsync(Guid customerId, CreateEndUserRequest request, CancellationToken cancellationToken = default);

    Task<EndUserSummaryDto> UpdateEndUserAsync(Guid customerId, Guid userId, UpdateEndUserRequest request, CancellationToken cancellationToken = default);

    Task DeleteEndUserAsync(Guid customerId, Guid userId, CancellationToken cancellationToken = default);

    Task<CustomerDetailDto?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerDomainDto>> ListCustomerDomainsAsync(Guid customerId, CancellationToken cancellationToken = default);
}
