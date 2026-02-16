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

public sealed class ResolverAdministrationService(
    HelpdeskDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IResolverAdministrationService
{
    public async Task<ResolverAssignmentOptionsDto> GetAssignmentOptionsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        EnsureCustomerAccess(customerId);

        IReadOnlyList<ResolverUserSummaryDto> users = await ListResolverUsersAsync(customerId, cancellationToken);
        List<ResolverGroupSummaryDto> groups = await dbContext.ResolverGroups
            .AsNoTracking()
            .Where(item => item.CustomerId == customerId)
            .OrderBy(item => item.Name)
            .Select(item => new ResolverGroupSummaryDto(
                item.Id,
                item.CustomerId,
                item.Name,
                item.IsActive,
                item.CreatedUtc,
                item.UpdatedUtc))
            .ToListAsync(cancellationToken);

        return new ResolverAssignmentOptionsDto(users, groups);
    }

    public async Task<IReadOnlyList<ResolverGroupSummaryDto>> ListResolverGroupsAsync(Guid? customerId, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();

        IQueryable<ResolverGroup> query = dbContext.ResolverGroups.AsNoTracking();
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
            .Select(item => new ResolverGroupSummaryDto(
                item.Id,
                item.CustomerId,
                item.Name,
                item.IsActive,
                item.CreatedUtc,
                item.UpdatedUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<ResolverGroupSummaryDto> CreateResolverGroupAsync(CreateResolverGroupRequest request, CancellationToken cancellationToken = default)
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

        string trimmedName = request.Name.Trim();
        bool exists = await dbContext.ResolverGroups
            .AnyAsync(
                item => item.CustomerId == request.CustomerId &&
                        item.Name.ToLower() == trimmedName.ToLower(),
                cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Resolver group '{trimmedName}' already exists for this customer.");
        }

        DateTime utcNow = DateTime.UtcNow;
        ResolverGroup group = new(Guid.NewGuid(), request.CustomerId, trimmedName, request.IsActive, utcNow);
        dbContext.ResolverGroups.Add(group);

        AddAuditEvent(
            group.CustomerId,
            tenantContextAccessor.Current.UserId,
            "resolver.group.created",
            new { resolverGroupId = group.Id, group.CustomerId, group.Name, group.IsActive });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToSummaryDto(group);
    }

    public async Task<ResolverGroupSummaryDto> UpdateResolverGroupAsync(Guid resolverGroupId, UpdateResolverGroupRequest request, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        ResolverGroup group = await dbContext.ResolverGroups
            .SingleOrDefaultAsync(item => item.Id == resolverGroupId, cancellationToken)
            ?? throw new KeyNotFoundException($"Resolver group '{resolverGroupId}' was not found.");

        EnsureCustomerAccess(group.CustomerId);

        string trimmedName = request.Name.Trim();
        bool duplicate = await dbContext.ResolverGroups
            .AnyAsync(
                item => item.Id != resolverGroupId &&
                        item.CustomerId == group.CustomerId &&
                        item.Name.ToLower() == trimmedName.ToLower(),
                cancellationToken);

        if (duplicate)
        {
            throw new InvalidOperationException($"Resolver group '{trimmedName}' already exists for this customer.");
        }

        DateTime utcNow = DateTime.UtcNow;
        group.Rename(trimmedName, utcNow);
        group.SetActive(request.IsActive, utcNow);

        AddAuditEvent(
            group.CustomerId,
            tenantContextAccessor.Current.UserId,
            "resolver.group.updated",
            new { resolverGroupId = group.Id, group.CustomerId, group.Name, group.IsActive });

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToSummaryDto(group);
    }

    public async Task DeleteResolverGroupAsync(Guid resolverGroupId, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();

        ResolverGroup group = await dbContext.ResolverGroups
            .SingleOrDefaultAsync(item => item.Id == resolverGroupId, cancellationToken)
            ?? throw new KeyNotFoundException($"Resolver group '{resolverGroupId}' was not found.");

        EnsureCustomerAccess(group.CustomerId);

        bool hasAssignedTickets = await dbContext.Tickets
            .AnyAsync(item => item.ResolverGroupId == resolverGroupId, cancellationToken);

        if (hasAssignedTickets)
        {
            throw new InvalidOperationException("Resolver group cannot be deleted while tickets are assigned to it.");
        }

        AddAuditEvent(
            group.CustomerId,
            tenantContextAccessor.Current.UserId,
            "resolver.group.deleted",
            new { resolverGroupId = group.Id, group.CustomerId, group.Name });

        dbContext.ResolverGroups.Remove(group);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ResolverUserSummaryDto>> ListResolverUsersAsync(Guid customerId, CancellationToken cancellationToken)
    {
        List<ResolverUserRow> rows = await (
            from user in dbContext.Users.AsNoTracking()
            join userRole in dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where role.Name == RoleNames.Technician || role.Name == RoleNames.MspAdmin
            where role.Name == RoleNames.MspAdmin || user.CustomerId == customerId
            select new ResolverUserRow(
                user.Id,
                user.CustomerId,
                user.Email ?? string.Empty,
                user.DisplayName,
                role.Name ?? string.Empty))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(item => item.Id)
            .Select(group => group
                .OrderByDescending(item => item.Role == RoleNames.MspAdmin)
                .ThenBy(item => item.Role)
                .First())
            .OrderBy(item => item.DisplayName)
            .ThenBy(item => item.Email)
            .Select(item => new ResolverUserSummaryDto(item.Id, item.CustomerId, item.Email, item.DisplayName, item.Role))
            .ToList();
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
            throw new TenantAccessDeniedException("Resolver administration requires technician or admin access.");
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

    private static ResolverGroupSummaryDto ToSummaryDto(ResolverGroup group)
    {
        return new ResolverGroupSummaryDto(
            group.Id,
            group.CustomerId,
            group.Name,
            group.IsActive,
            group.CreatedUtc,
            group.UpdatedUtc);
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

    private sealed record ResolverUserRow(
        Guid Id,
        Guid? CustomerId,
        string Email,
        string DisplayName,
        string Role);
}
