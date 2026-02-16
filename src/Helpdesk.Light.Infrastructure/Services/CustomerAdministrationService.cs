using System.Text.Json;
using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Errors;
using Helpdesk.Light.Domain.Entities;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;
using Helpdesk.Light.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class CustomerAdministrationService(
    HelpdeskDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    UserManager<ApplicationUser>? userManager = null) : ICustomerAdministrationService
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
        AddAuditEvent(
            customer.Id,
            tenantContextAccessor.Current.UserId,
            "admin.customer.created",
            new { customerId = customer.Id, customer.Name, customer.IsActive });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CustomerSummaryDto(customer.Id, customer.Name, customer.IsActive, 0);
    }

    public async Task<CustomerSummaryDto> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        Customer customer = await dbContext.Customers
            .SingleOrDefaultAsync(item => item.Id == customerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{customerId}' was not found.");

        customer.Rename(request.Name);
        customer.SetActive(request.IsActive);

        AddAuditEvent(
            customer.Id,
            tenantContextAccessor.Current.UserId,
            "admin.customer.updated",
            new { customerId = customer.Id, customer.Name, customer.IsActive });

        await dbContext.SaveChangesAsync(cancellationToken);

        int domainCount = await dbContext.CustomerDomains.CountAsync(item => item.CustomerId == customerId, cancellationToken);
        return new CustomerSummaryDto(customer.Id, customer.Name, customer.IsActive, domainCount);
    }

    public async Task DeleteCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        Customer customer = await dbContext.Customers
            .Include(item => item.Domains)
            .SingleOrDefaultAsync(item => item.Id == customerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{customerId}' was not found.");

        bool hasUsers = await dbContext.Users.AnyAsync(item => item.CustomerId == customerId, cancellationToken);
        bool hasTickets = await dbContext.Tickets.AnyAsync(item => item.CustomerId == customerId, cancellationToken);
        bool hasKnowledgeArticles = await dbContext.KnowledgeArticles.AnyAsync(item => item.CustomerId == customerId, cancellationToken);

        if (hasUsers || hasTickets || hasKnowledgeArticles)
        {
            throw new InvalidOperationException("Customer cannot be deleted while users, tickets, or knowledge articles still exist.");
        }

        AddAuditEvent(
            customer.Id,
            tenantContextAccessor.Current.UserId,
            "admin.customer.deleted",
            new { customerId = customer.Id, customer.Name });

        dbContext.Customers.Remove(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
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
        AddAuditEvent(
            customer.Id,
            tenantContextAccessor.Current.UserId,
            "admin.customer.domain.added",
            new { customerId = customer.Id, domainId = domain.Id, domain.Domain, domain.IsPrimary });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CustomerDomainDto(domain.Id, domain.CustomerId, domain.Domain, domain.IsPrimary);
    }

    public async Task<IReadOnlyList<EndUserSummaryDto>> ListEndUsersAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        Guid endUserRoleId = await GetRoleIdAsync(RoleNames.EndUser, cancellationToken);

        return await dbContext.Users
            .AsNoTracking()
            .Where(item => item.CustomerId == customerId)
            .Where(item => dbContext.UserRoles.Any(role => role.UserId == item.Id && role.RoleId == endUserRoleId))
            .OrderBy(item => item.Email)
            .Select(item => new EndUserSummaryDto(
                item.Id,
                item.CustomerId ?? Guid.Empty,
                item.Email ?? string.Empty,
                item.DisplayName,
                item.EmailConfirmed))
            .ToListAsync(cancellationToken);
    }

    public async Task<EndUserSummaryDto> CreateEndUserAsync(Guid customerId, CreateEndUserRequest request, CancellationToken cancellationToken = default)
    {
        UserManager<ApplicationUser> identityUserManager = RequireUserManager();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DisplayName);

        _ = await dbContext.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == customerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{customerId}' was not found.");

        await EnsureEmailMatchesCustomerDomainAsync(request.Email, customerId, cancellationToken);

        ApplicationUser? existing = await identityUserManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            throw new InvalidOperationException($"A user with email '{request.Email}' already exists.");
        }

        ApplicationUser created = new()
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            CustomerId = customerId,
            DisplayName = request.DisplayName.Trim()
        };

        string temporaryPassword = $"Tmp!{Guid.NewGuid():N}aA1";
        IdentityResult createResult = await identityUserManager.CreateAsync(created, temporaryPassword);
        EnsureSucceeded(createResult, $"Failed to create end user '{request.Email}'.");

        IdentityResult roleResult = await identityUserManager.AddToRoleAsync(created, RoleNames.EndUser);
        EnsureSucceeded(roleResult, $"Failed to assign EndUser role to '{request.Email}'.");

        AddAuditEvent(
            customerId,
            tenantContextAccessor.Current.UserId,
            "admin.end_user.created",
            new { customerId, userId = created.Id, created.Email, created.DisplayName });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new EndUserSummaryDto(created.Id, customerId, created.Email ?? string.Empty, created.DisplayName, created.EmailConfirmed);
    }

    public async Task<EndUserSummaryDto> UpdateEndUserAsync(Guid customerId, Guid userId, UpdateEndUserRequest request, CancellationToken cancellationToken = default)
    {
        UserManager<ApplicationUser> identityUserManager = RequireUserManager();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DisplayName);

        ApplicationUser user = await identityUserManager.Users
            .SingleOrDefaultAsync(item => item.Id == userId && item.CustomerId == customerId, cancellationToken)
            ?? throw new KeyNotFoundException($"End user '{userId}' was not found for customer '{customerId}'.");

        await EnsureUserIsEndUserAsync(user);
        await EnsureEmailMatchesCustomerDomainAsync(request.Email, customerId, cancellationToken);

        ApplicationUser? existingByEmail = await identityUserManager.FindByEmailAsync(request.Email);
        if (existingByEmail is not null && existingByEmail.Id != user.Id)
        {
            throw new InvalidOperationException($"A different user already uses '{request.Email}'.");
        }

        user.Email = request.Email.Trim();
        user.UserName = request.Email.Trim();
        user.DisplayName = request.DisplayName.Trim();
        user.EmailConfirmed = request.EmailConfirmed;

        IdentityResult updateResult = await identityUserManager.UpdateAsync(user);
        EnsureSucceeded(updateResult, $"Failed to update end user '{userId}'.");

        AddAuditEvent(
            customerId,
            tenantContextAccessor.Current.UserId,
            "admin.end_user.updated",
            new { customerId, userId = user.Id, user.Email, user.DisplayName, user.EmailConfirmed });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new EndUserSummaryDto(user.Id, customerId, user.Email ?? string.Empty, user.DisplayName, user.EmailConfirmed);
    }

    public async Task DeleteEndUserAsync(Guid customerId, Guid userId, CancellationToken cancellationToken = default)
    {
        UserManager<ApplicationUser> identityUserManager = RequireUserManager();
        ApplicationUser user = await identityUserManager.Users
            .SingleOrDefaultAsync(item => item.Id == userId && item.CustomerId == customerId, cancellationToken)
            ?? throw new KeyNotFoundException($"End user '{userId}' was not found for customer '{customerId}'.");

        await EnsureUserIsEndUserAsync(user);

        bool hasTickets = await dbContext.Tickets.AnyAsync(item => item.CreatedByUserId == user.Id, cancellationToken);
        if (hasTickets)
        {
            throw new InvalidOperationException("End user cannot be deleted while they still own tickets.");
        }

        AddAuditEvent(
            customerId,
            tenantContextAccessor.Current.UserId,
            "admin.end_user.deleted",
            new { customerId, userId = user.Id, user.Email });

        IdentityResult deleteResult = await identityUserManager.DeleteAsync(user);
        EnsureSucceeded(deleteResult, $"Failed to delete end user '{userId}'.");

        await dbContext.SaveChangesAsync(cancellationToken);
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

    private async Task EnsureEmailMatchesCustomerDomainAsync(string email, Guid customerId, CancellationToken cancellationToken)
    {
        string domain = CustomerDomain.ExtractDomainFromEmail(email);
        bool mappedToCustomer = await dbContext.CustomerDomains
            .AsNoTracking()
            .AnyAsync(item => item.CustomerId == customerId && item.Domain == domain, cancellationToken);

        if (!mappedToCustomer)
        {
            throw new InvalidOperationException("End user email domain must be mapped to the selected customer.");
        }
    }

    private async Task EnsureUserIsEndUserAsync(ApplicationUser user)
    {
        bool isEndUser = await RequireUserManager().IsInRoleAsync(user, RoleNames.EndUser);
        if (!isEndUser)
        {
            throw new InvalidOperationException("Selected user is not an EndUser account.");
        }
    }

    private async Task<Guid> GetRoleIdAsync(string roleName, CancellationToken cancellationToken)
    {
        return await dbContext.Roles
            .Where(item => item.Name == roleName)
            .Select(item => item.Id)
            .SingleAsync(cancellationToken);
    }

    private static CustomerDetailDto ToDetail(Customer customer)
    {
        List<CustomerDomainDto> domains = customer.Domains
            .OrderBy(item => item.Domain)
            .Select(item => new CustomerDomainDto(item.Id, item.CustomerId, item.Domain, item.IsPrimary))
            .ToList();

        return new CustomerDetailDto(customer.Id, customer.Name, customer.IsActive, domains);
    }

    private void AddAuditEvent(Guid customerId, Guid? actorUserId, string eventType, object payload)
    {
        dbContext.AuditEvents.Add(new AuditEvent(
            Guid.NewGuid(),
            customerId,
            actorUserId,
            eventType,
            JsonSerializer.Serialize(payload),
            DateTime.UtcNow));
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        string errors = string.Join("; ", result.Errors.Select(item => item.Description));
        throw new InvalidOperationException($"{message} Errors: {errors}");
    }

    private UserManager<ApplicationUser> RequireUserManager()
    {
        return userManager ?? throw new InvalidOperationException("User manager is required for end-user management operations.");
    }
}
