using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Errors;
using Helpdesk.Light.Domain.Entities;
using Helpdesk.Light.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class CustomerAdministrationService(
    HelpdeskDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : ICustomerAdministrationService
{
    public async Task<IReadOnlyList<CustomerSummaryDto>> ListCustomersAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Customers
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new CustomerSummaryDto(item.Id, item.Name, item.IsActive, item.Domains.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerSummaryDto> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        Customer customer = new(Guid.NewGuid(), request.Name, request.IsActive);
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CustomerSummaryDto(customer.Id, customer.Name, customer.IsActive, 0);
    }

    public async Task<CustomerDomainDto> AddDomainAsync(Guid customerId, AddCustomerDomainRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Domain);

        Customer customer = await dbContext.Customers
            .Include(item => item.Domains)
            .SingleOrDefaultAsync(item => item.Id == customerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{customerId}' was not found.");

        CustomerDomain domain = customer.AddDomain(Guid.NewGuid(), request.Domain, request.IsPrimary);
        dbContext.CustomerDomains.Add(domain);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CustomerDomainDto(domain.Id, domain.CustomerId, domain.Domain, domain.IsPrimary);
    }

    public async Task<CustomerDetailDto?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        EnsureCustomerAccess(customerId);

        Customer? customer = await dbContext.Customers
            .AsNoTracking()
            .Include(item => item.Domains)
            .SingleOrDefaultAsync(item => item.Id == customerId, cancellationToken);

        return customer is null ? null : ToDetail(customer);
    }

    public async Task<IReadOnlyList<CustomerDomainDto>> ListCustomerDomainsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        EnsureCustomerAccess(customerId);

        return await dbContext.CustomerDomains
            .AsNoTracking()
            .Where(item => item.CustomerId == customerId)
            .OrderBy(item => item.Domain)
            .Select(item => new CustomerDomainDto(item.Id, item.CustomerId, item.Domain, item.IsPrimary))
            .ToListAsync(cancellationToken);
    }

    private void EnsureCustomerAccess(Guid customerId)
    {
        TenantAccessContext context = tenantContextAccessor.Current;
        if (!context.CanAccessCustomer(customerId))
        {
            throw new TenantAccessDeniedException("Tenant boundary violation detected.");
        }
    }

    private static CustomerDetailDto ToDetail(Customer customer)
    {
        List<CustomerDomainDto> domains = customer.Domains
            .OrderBy(item => item.Domain)
            .Select(item => new CustomerDomainDto(item.Id, item.CustomerId, item.Domain, item.IsPrimary))
            .ToList();

        return new CustomerDetailDto(customer.Id, customer.Name, customer.IsActive, domains);
    }
}
