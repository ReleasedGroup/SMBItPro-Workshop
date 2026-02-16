using System.Text.Json;
using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Errors;
using Helpdesk.Light.Domain.Entities;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class TicketCategoryAdministrationService(
    HelpdeskDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : ITicketCategoryAdministrationService
{
    public async Task<IReadOnlyList<TicketCategorySummaryDto>> ListTicketCategoriesAsync(Guid? customerId, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();

        IQueryable<TicketCategory> query = dbContext.TicketCategories.AsNoTracking();
        TenantAccessContext context = tenantContextAccessor.Current;

        if (customerId.HasValue)
        {
            EnsureCustomerAccess(customerId.Value);
            query = query.Where(item => item.CustomerId == customerId.Value);
        }
        else if (!context.IsMspAdmin)
        {
            if (!context.CustomerId.HasValue)
            {
                throw new UnauthorizedAccessException("Authenticated technician is missing tenant context.");
            }

            query = query.Where(item => item.CustomerId == context.CustomerId.Value);
        }

        return await query
            .OrderBy(item => item.Name)
            .Select(item => new TicketCategorySummaryDto(
                item.Id,
                item.CustomerId,
                item.Name,
                item.IsActive,
                item.ResolverGroupId,
                item.CreatedUtc,
                item.UpdatedUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<TicketCategorySummaryDto> CreateTicketCategoryAsync(CreateTicketCategoryRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        EnsureCustomerAccess(request.CustomerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        bool customerExists = await dbContext.Customers
            .AsNoTracking()
            .AnyAsync(item => item.Id == request.CustomerId, cancellationToken);

        if (!customerExists)
        {
            throw new KeyNotFoundException($"Customer '{request.CustomerId}' was not found.");
        }

        Guid? resolverGroupId = await ValidateResolverGroupMappingAsync(request.CustomerId, request.ResolverGroupId, cancellationToken);

        string trimmedName = request.Name.Trim();
        bool exists = await dbContext.TicketCategories
            .AnyAsync(
                item => item.CustomerId == request.CustomerId &&
                        item.Name.ToLower() == trimmedName.ToLower(),
                cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Ticket category '{trimmedName}' already exists for this customer.");
        }

        DateTime utcNow = DateTime.UtcNow;
        TicketCategory category = new(Guid.NewGuid(), request.CustomerId, trimmedName, request.IsActive, resolverGroupId, utcNow);
        dbContext.TicketCategories.Add(category);

        AddAuditEvent(
            category.CustomerId,
            tenantContextAccessor.Current.UserId,
            "ticket.category.created",
            new
            {
                ticketCategoryId = category.Id,
                category.CustomerId,
                category.Name,
                category.IsActive,
                category.ResolverGroupId
            });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToSummary(category);
    }

    public async Task<TicketCategorySummaryDto> UpdateTicketCategoryAsync(Guid ticketCategoryId, UpdateTicketCategoryRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        TicketCategory category = await dbContext.TicketCategories
            .SingleOrDefaultAsync(item => item.Id == ticketCategoryId, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket category '{ticketCategoryId}' was not found.");

        EnsureCustomerAccess(category.CustomerId);

        Guid? resolverGroupId = await ValidateResolverGroupMappingAsync(category.CustomerId, request.ResolverGroupId, cancellationToken);

        string trimmedName = request.Name.Trim();
        bool duplicate = await dbContext.TicketCategories
            .AnyAsync(
                item => item.Id != ticketCategoryId &&
                        item.CustomerId == category.CustomerId &&
                        item.Name.ToLower() == trimmedName.ToLower(),
                cancellationToken);

        if (duplicate)
        {
            throw new InvalidOperationException($"Ticket category '{trimmedName}' already exists for this customer.");
        }

        DateTime utcNow = DateTime.UtcNow;
        category.Rename(trimmedName, utcNow);
        category.SetActive(request.IsActive, utcNow);
        category.SetResolverGroup(resolverGroupId, utcNow);

        AddAuditEvent(
            category.CustomerId,
            tenantContextAccessor.Current.UserId,
            "ticket.category.updated",
            new
            {
                ticketCategoryId = category.Id,
                category.CustomerId,
                category.Name,
                category.IsActive,
                category.ResolverGroupId
            });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToSummary(category);
    }

    public async Task DeleteTicketCategoryAsync(Guid ticketCategoryId, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();

        TicketCategory category = await dbContext.TicketCategories
            .SingleOrDefaultAsync(item => item.Id == ticketCategoryId, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket category '{ticketCategoryId}' was not found.");

        EnsureCustomerAccess(category.CustomerId);

        AddAuditEvent(
            category.CustomerId,
            tenantContextAccessor.Current.UserId,
            "ticket.category.deleted",
            new { ticketCategoryId = category.Id, category.CustomerId, category.Name });

        dbContext.TicketCategories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid?> ValidateResolverGroupMappingAsync(Guid customerId, Guid? resolverGroupId, CancellationToken cancellationToken)
    {
        if (!resolverGroupId.HasValue)
        {
            return null;
        }

        ResolverGroup? group = await dbContext.ResolverGroups
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == resolverGroupId.Value && item.CustomerId == customerId, cancellationToken);

        if (group is null)
        {
            throw new InvalidOperationException("Resolver group mapping is invalid for this customer.");
        }

        if (!group.IsActive)
        {
            throw new InvalidOperationException("Resolver group mapping requires an active resolver group.");
        }

        return group.Id;
    }

    private static TicketCategorySummaryDto ToSummary(TicketCategory category)
    {
        return new TicketCategorySummaryDto(
            category.Id,
            category.CustomerId,
            category.Name,
            category.IsActive,
            category.ResolverGroupId,
            category.CreatedUtc,
            category.UpdatedUtc);
    }

    private void EnsureCanManage()
    {
        TenantAccessContext context = tenantContextAccessor.Current;
        if (!context.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        if (context.Role is not RoleNames.Technician and not RoleNames.MspAdmin)
        {
            throw new TenantAccessDeniedException("Ticket category administration requires technician or admin access.");
        }
    }

    private void EnsureCustomerAccess(Guid customerId)
    {
        TenantAccessContext context = tenantContextAccessor.Current;
        if (context.IsMspAdmin)
        {
            return;
        }

        if (!context.CustomerId.HasValue || context.CustomerId.Value != customerId)
        {
            throw new TenantAccessDeniedException("Tenant boundary violation detected.");
        }
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
}
