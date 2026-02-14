using System.Text.Json;
using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Abstractions.Ai;
using Helpdesk.Light.Application.Abstractions.Email;
using Helpdesk.Light.Application.Abstractions.Tickets;
using Helpdesk.Light.Application.Contracts.Ai;
using Helpdesk.Light.Application.Contracts.Tickets;
using Helpdesk.Light.Application.Errors;
using Helpdesk.Light.Domain.Ai;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;
using Helpdesk.Light.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class TicketService(
    HelpdeskDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ITicketAccessGuard ticketAccessGuard,
    IAttachmentStorage attachmentStorage,
    IOutboundEmailService outboundEmailService,
    IAiTicketAgentService aiTicketAgentService) : ITicketService
{
    public async Task<TicketSummaryDto> CreateTicketAsync(CreateTicketRequest request, CancellationToken cancellationToken = default)
    {
        TenantAccessContext context = tenantContextAccessor.Current;
        if (!context.IsAuthenticated || !context.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        Guid customerId = ResolveCustomerId(request.CustomerId, context);
        Guid userId = context.UserId.Value;
        DateTime utcNow = DateTime.UtcNow;

        TicketPriority priority = request.Priority;
        Ticket ticket = new(Guid.NewGuid(), customerId, userId, TicketChannel.Web, request.Subject, request.Description, priority, utcNow);
        dbContext.Tickets.Add(ticket);

        TicketMessage initialMessage = new(
            Guid.NewGuid(),
            ticket.Id,
            GetAuthorTypeFromRole(context.Role),
            userId,
            request.Description,
            TicketMessageSource.Web,
            null,
            utcNow);

        dbContext.TicketMessages.Add(initialMessage);
        AddAuditEvent(customerId, userId, "ticket.created", new { ticketId = ticket.Id, ticket.ReferenceCode, ticket.Priority, ticket.Subject, ticket.Channel });

        await dbContext.SaveChangesAsync(cancellationToken);

        await outboundEmailService.QueueAsync(
            customerId,
            ticket.Id,
            context.Email,
            $"[{ticket.ReferenceCode}] Ticket received",
            "Your ticket has been created and is now in the queue.",
            $"ticket-created:{ticket.Id}:{userId}",
            cancellationToken);

        await TryRunAiAsync(ticket.Id, "TicketCreated", cancellationToken);

        TicketAiSuggestion? latestSuggestion = await GetLatestSuggestionAsync(ticket.Id, cancellationToken);
        return TicketMapper.ToSummary(ticket, latestSuggestion);
    }

    public async Task<IReadOnlyList<TicketSummaryDto>> ListTicketsAsync(TicketFilterRequest request, CancellationToken cancellationToken = default)
    {
        TenantAccessContext context = tenantContextAccessor.Current;
        if (!context.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        IQueryable<Ticket> query = dbContext.Tickets.AsNoTracking();
        query = ApplyAccessScope(query, context);

        if (request.Status.HasValue)
        {
            query = query.Where(item => item.Status == request.Status.Value);
        }

        if (request.Priority.HasValue)
        {
            query = query.Where(item => item.Priority == request.Priority.Value);
        }

        if (request.CustomerId.HasValue)
        {
            query = query.Where(item => item.CustomerId == request.CustomerId.Value);
        }

        if (request.AssignedToUserId.HasValue)
        {
            query = query.Where(item => item.AssignedToUserId == request.AssignedToUserId.Value);
        }

        int take = Math.Clamp(request.Take, 1, 300);
        List<Ticket> tickets = await query
            .OrderByDescending(item => item.UpdatedUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        HashSet<Guid> ticketIds = tickets.Select(item => item.Id).ToHashSet();
        Dictionary<Guid, TicketAiSuggestion> latestSuggestions = await dbContext.TicketAiSuggestions
            .AsNoTracking()
            .Where(item => ticketIds.Contains(item.TicketId))
            .OrderByDescending(item => item.CreatedUtc)
            .GroupBy(item => item.TicketId)
            .Select(group => group.First())
            .ToDictionaryAsync(item => item.TicketId, cancellationToken);

        return tickets
            .Select(ticket => TicketMapper.ToSummary(ticket, latestSuggestions.GetValueOrDefault(ticket.Id)))
            .ToList();
    }

    public async Task<TicketDetailDto?> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        Ticket? ticket = await dbContext.Tickets.AsNoTracking().SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return null;
        }

        TenantAccessContext context = tenantContextAccessor.Current;
        if (!ticketAccessGuard.CanRead(ticket, context))
        {
            throw new TenantAccessDeniedException("Cannot access ticket outside tenant boundary.");
        }

        List<TicketMessageDto> messages = await dbContext.TicketMessages
            .AsNoTracking()
            .Where(item => item.TicketId == ticketId)
            .OrderBy(item => item.CreatedUtc)
            .Select(item => TicketMapper.ToDto(item))
            .ToListAsync(cancellationToken);

        List<TicketAttachmentDto> attachments = await dbContext.TicketAttachments
            .AsNoTracking()
            .Where(item => item.TicketId == ticketId)
            .OrderBy(item => item.CreatedUtc)
            .Select(item => TicketMapper.ToDto(item))
            .ToListAsync(cancellationToken);

        TicketAiSuggestion? latestSuggestion = await GetLatestSuggestionAsync(ticketId, cancellationToken);
        TicketSummaryDto summary = TicketMapper.ToSummary(ticket, latestSuggestion);

        return new TicketDetailDto(summary, messages, attachments, latestSuggestion is null ? null : TicketMapper.ToDto(latestSuggestion));
    }

    public async Task<TicketMessageDto> AddMessageAsync(Guid ticketId, TicketMessageCreateRequest request, CancellationToken cancellationToken = default)
    {
        Ticket ticket = await dbContext.Tickets.SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket '{ticketId}' was not found.");

        TenantAccessContext context = tenantContextAccessor.Current;
        if (!ticketAccessGuard.CanWrite(ticket, context))
        {
            throw new TenantAccessDeniedException("Cannot update ticket outside tenant boundary.");
        }

        if (!context.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Authenticated user id is required.");
        }

        DateTime utcNow = DateTime.UtcNow;
        TicketAuthorType authorType = GetAuthorTypeFromRole(context.Role);

        TicketMessage message = new(
            Guid.NewGuid(),
            ticket.Id,
            authorType,
            context.UserId,
            request.Body,
            TicketMessageSource.Web,
            null,
            utcNow);

        dbContext.TicketMessages.Add(message);
        ticket.SetSummary(ticket.Summary, utcNow);

        if (authorType == TicketAuthorType.Technician && ticket.Status == TicketStatus.InProgress)
        {
            ticket.TransitionStatus(TicketStatus.WaitingCustomer, utcNow);
        }

        if (authorType == TicketAuthorType.EndUser && ticket.Status == TicketStatus.WaitingCustomer)
        {
            ticket.TransitionStatus(TicketStatus.InProgress, utcNow);
        }

        AddAuditEvent(ticket.CustomerId, context.UserId, "ticket.message.added", new { ticketId = ticket.Id, messageId = message.Id, authorType = authorType.ToString() });

        await dbContext.SaveChangesAsync(cancellationToken);

        Guid notifyUserId = ticket.CreatedByUserId == context.UserId.Value
            ? ticket.AssignedToUserId ?? ticket.CreatedByUserId
            : ticket.CreatedByUserId;

        string? notifyEmail = await GetUserEmailAsync(notifyUserId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(notifyEmail))
        {
            await outboundEmailService.QueueAsync(
                ticket.CustomerId,
                ticket.Id,
                notifyEmail,
                $"[{ticket.ReferenceCode}] Ticket updated",
                request.Body,
                $"ticket-message:{ticket.Id}:{message.Id}",
                cancellationToken);
        }

        if (authorType == TicketAuthorType.EndUser)
        {
            await TryRunAiAsync(ticket.Id, "TicketUpdated", cancellationToken);
        }

        return TicketMapper.ToDto(message);
    }

    public async Task<TicketSummaryDto> AssignTicketAsync(Guid ticketId, TicketAssignRequest request, CancellationToken cancellationToken = default)
    {
        Ticket ticket = await dbContext.Tickets.SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket '{ticketId}' was not found.");

        TenantAccessContext context = tenantContextAccessor.Current;
        if (!ticketAccessGuard.CanManage(ticket, context))
        {
            throw new TenantAccessDeniedException("Cannot assign ticket outside tenant boundary.");
        }

        ticket.Assign(request.AssignedToUserId, DateTime.UtcNow);
        AddAuditEvent(ticket.CustomerId, context.UserId, "ticket.assigned", new { ticketId = ticket.Id, assignedTo = request.AssignedToUserId });
        await dbContext.SaveChangesAsync(cancellationToken);

        TicketAiSuggestion? latestSuggestion = await GetLatestSuggestionAsync(ticket.Id, cancellationToken);
        return TicketMapper.ToSummary(ticket, latestSuggestion);
    }

    public async Task<TicketSummaryDto> UpdateStatusAsync(Guid ticketId, TicketStatusUpdateRequest request, CancellationToken cancellationToken = default)
    {
        Ticket ticket = await dbContext.Tickets.SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket '{ticketId}' was not found.");

        TenantAccessContext context = tenantContextAccessor.Current;
        if (!ticketAccessGuard.CanManage(ticket, context))
        {
            throw new TenantAccessDeniedException("Cannot transition ticket outside tenant boundary.");
        }

        ticket.TransitionStatus(request.Status, DateTime.UtcNow);
        AddAuditEvent(ticket.CustomerId, context.UserId, "ticket.status.updated", new { ticketId = ticket.Id, status = request.Status.ToString() });

        await dbContext.SaveChangesAsync(cancellationToken);

        string? creatorEmail = await GetUserEmailAsync(ticket.CreatedByUserId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(creatorEmail))
        {
            await outboundEmailService.QueueAsync(
                ticket.CustomerId,
                ticket.Id,
                creatorEmail,
                $"[{ticket.ReferenceCode}] Status changed to {ticket.Status}",
                $"Ticket status updated to {ticket.Status}.",
                $"ticket-status:{ticket.Id}:{ticket.Status}",
                cancellationToken);
        }

        TicketAiSuggestion? latestSuggestion = await GetLatestSuggestionAsync(ticket.Id, cancellationToken);
        return TicketMapper.ToSummary(ticket, latestSuggestion);
    }

    public async Task<TicketSummaryDto> UpdateTriageAsync(Guid ticketId, TicketTriageUpdateRequest request, CancellationToken cancellationToken = default)
    {
        Ticket ticket = await dbContext.Tickets.SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket '{ticketId}' was not found.");

        TenantAccessContext context = tenantContextAccessor.Current;
        if (!ticketAccessGuard.CanManage(ticket, context))
        {
            throw new TenantAccessDeniedException("Cannot update triage fields outside tenant boundary.");
        }

        DateTime utcNow = DateTime.UtcNow;
        ticket.SetPriority(request.Priority, utcNow);
        ticket.SetCategory(request.Category, utcNow);
        AddAuditEvent(ticket.CustomerId, context.UserId, "ticket.triage.updated", new { ticketId = ticket.Id, request.Priority, request.Category });

        await dbContext.SaveChangesAsync(cancellationToken);

        TicketAiSuggestion? latestSuggestion = await GetLatestSuggestionAsync(ticket.Id, cancellationToken);
        return TicketMapper.ToSummary(ticket, latestSuggestion);
    }

    public async Task<TicketAttachmentDto> UploadAttachmentAsync(Guid ticketId, AttachmentUploadRequest request, CancellationToken cancellationToken = default)
    {
        Ticket ticket = await dbContext.Tickets.SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket '{ticketId}' was not found.");

        TenantAccessContext context = tenantContextAccessor.Current;
        if (!ticketAccessGuard.CanWrite(ticket, context))
        {
            throw new TenantAccessDeniedException("Cannot upload attachment outside tenant boundary.");
        }

        string storagePath = await attachmentStorage.SaveAsync(ticket.Id, request, cancellationToken);
        TicketAttachment attachment = new(
            Guid.NewGuid(),
            ticket.Id,
            request.FileName,
            request.ContentType,
            request.SizeBytes,
            storagePath,
            context.UserId,
            DateTime.UtcNow);

        dbContext.TicketAttachments.Add(attachment);
        AddAuditEvent(ticket.CustomerId, context.UserId, "ticket.attachment.uploaded", new { ticketId = ticket.Id, attachmentId = attachment.Id, request.FileName });

        await dbContext.SaveChangesAsync(cancellationToken);
        return TicketMapper.ToDto(attachment);
    }

    public async Task<AttachmentDownloadResult?> DownloadAttachmentAsync(Guid ticketId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        Ticket ticket = await dbContext.Tickets.AsNoTracking().SingleOrDefaultAsync(item => item.Id == ticketId, cancellationToken)
            ?? throw new KeyNotFoundException($"Ticket '{ticketId}' was not found.");

        TenantAccessContext context = tenantContextAccessor.Current;
        if (!ticketAccessGuard.CanRead(ticket, context))
        {
            throw new TenantAccessDeniedException("Cannot download attachment outside tenant boundary.");
        }

        TicketAttachment? attachment = await dbContext.TicketAttachments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == attachmentId && item.TicketId == ticketId, cancellationToken);

        if (attachment is null)
        {
            return null;
        }

        Stream? content = await attachmentStorage.OpenReadAsync(attachment.StoragePath, cancellationToken);
        return content is null ? null : new AttachmentDownloadResult(content, attachment.ContentType, attachment.FileName);
    }

    private Guid ResolveCustomerId(Guid? requestedCustomerId, TenantAccessContext context)
    {
        if (context.IsMspAdmin)
        {
            if (!requestedCustomerId.HasValue || requestedCustomerId.Value == Guid.Empty)
            {
                throw new InvalidOperationException("Admin ticket creation must include customer id.");
            }

            return requestedCustomerId.Value;
        }

        if (!context.CustomerId.HasValue)
        {
            throw new UnauthorizedAccessException("Authenticated user is missing tenant context.");
        }

        if (requestedCustomerId.HasValue && requestedCustomerId.Value != context.CustomerId.Value)
        {
            throw new TenantAccessDeniedException("Cannot create ticket for another customer tenant.");
        }

        return context.CustomerId.Value;
    }

    private IQueryable<Ticket> ApplyAccessScope(IQueryable<Ticket> query, TenantAccessContext context)
    {
        if (context.IsMspAdmin)
        {
            return query;
        }

        if (!context.CustomerId.HasValue)
        {
            return query.Where(_ => false);
        }

        query = query.Where(item => item.CustomerId == context.CustomerId.Value);

        if (context.Role == RoleNames.EndUser && context.UserId.HasValue)
        {
            query = query.Where(item => item.CreatedByUserId == context.UserId.Value);
        }

        return query;
    }

    private static TicketAuthorType GetAuthorTypeFromRole(string role)
    {
        return role switch
        {
            RoleNames.EndUser => TicketAuthorType.EndUser,
            RoleNames.Technician => TicketAuthorType.Technician,
            RoleNames.MspAdmin => TicketAuthorType.Technician,
            _ => TicketAuthorType.System
        };
    }

    private async Task<string?> GetUserEmailAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => item.Email)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private void AddAuditEvent(Guid? customerId, Guid? actorUserId, string eventType, object payload)
    {
        string payloadJson = JsonSerializer.Serialize(payload);
        dbContext.AuditEvents.Add(new AuditEvent(Guid.NewGuid(), customerId, actorUserId, eventType, payloadJson, DateTime.UtcNow));
    }

    private async Task<TicketAiSuggestion?> GetLatestSuggestionAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        return await dbContext.TicketAiSuggestions
            .AsNoTracking()
            .Where(item => item.TicketId == ticketId)
            .OrderByDescending(item => item.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task TryRunAiAsync(Guid ticketId, string trigger, CancellationToken cancellationToken)
    {
        try
        {
            await aiTicketAgentService.RunForTicketAsync(new AiRunRequest(ticketId, trigger), cancellationToken);
        }
        catch
        {
            // AI failures should not block ticket workflows; the run service persists failures when possible.
        }
    }
}
