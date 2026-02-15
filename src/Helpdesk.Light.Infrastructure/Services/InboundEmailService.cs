using System.Text.Json;
using System.Text.RegularExpressions;
using Helpdesk.Light.Application.Abstractions.Ai;
using Helpdesk.Light.Application.Abstractions.Email;
using Helpdesk.Light.Application.Abstractions.Tickets;
using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts.Ai;
using Helpdesk.Light.Application.Contracts.Email;
using Helpdesk.Light.Application.Contracts.Tickets;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;
using Helpdesk.Light.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class InboundEmailService(
    HelpdeskDbContext dbContext,
    ITenantResolutionService tenantResolutionService,
    IAttachmentStorage attachmentStorage,
    IAiTicketAgentService aiTicketAgentService,
    UserManager<ApplicationUser> userManager) : IInboundEmailService
{
    public async Task<InboundEmailProcessResult> ProcessAsync(InboundEmailRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SenderEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Subject);

        bool duplicate = await dbContext.TicketMessages.AnyAsync(item => item.ExternalMessageId == request.MessageId, cancellationToken);
        if (duplicate)
        {
            return new InboundEmailProcessResult(true, true, null, null, "Duplicate message id ignored.");
        }

        var resolution = await tenantResolutionService.ResolveSenderEmailAsync(
            new Application.Contracts.ResolveSenderRequest(request.SenderEmail, request.Subject),
            cancellationToken);

        if (!resolution.IsMapped || !resolution.CustomerId.HasValue)
        {
            return new InboundEmailProcessResult(false, false, null, resolution.UnmappedQueueItemId, "Sender domain not mapped.");
        }

        Guid customerId = resolution.CustomerId.Value;
        ApplicationUser senderUser = await EnsureSenderUserAsync(customerId, request.SenderEmail, cancellationToken);

        string body = GetBodyText(request);
        DateTime receivedUtc = request.ReceivedUtc ?? DateTime.UtcNow;
        string? referenceCode = TicketReferenceParser.ExtractReferenceCode(request.Subject);

        Ticket? existingTicket = referenceCode is null
            ? null
            : await dbContext.Tickets.SingleOrDefaultAsync(item => item.ReferenceCode == referenceCode && item.CustomerId == customerId, cancellationToken);

        if (existingTicket is not null)
        {
            TicketMessage reply = new(
                Guid.NewGuid(),
                existingTicket.Id,
                TicketAuthorType.EndUser,
                senderUser.Id,
                body,
                TicketMessageSource.Email,
                request.MessageId,
                receivedUtc);

            dbContext.TicketMessages.Add(reply);

            if (existingTicket.Status == TicketStatus.WaitingCustomer)
            {
                existingTicket.TransitionStatus(TicketStatus.InProgress, receivedUtc);
            }

            await SaveInboundAttachmentsAsync(existingTicket.Id, senderUser.Id, request.Attachments, cancellationToken);
            AddAuditEvent(
                customerId,
                senderUser.Id,
                "ticket.email.reply.ingested",
                new { ticketId = existingTicket.Id, messageId = request.MessageId, sender = request.SenderEmail });
            await dbContext.SaveChangesAsync(cancellationToken);

            await aiTicketAgentService.RunForTicketAsync(new AiRunRequest(existingTicket.Id, "InboundEmailReply"), cancellationToken);
            return new InboundEmailProcessResult(false, true, existingTicket.Id, null, "Message appended to existing ticket.");
        }

        string subject = request.Subject;
        Ticket ticket = new(
            Guid.NewGuid(),
            customerId,
            senderUser.Id,
            TicketChannel.Email,
            subject,
            body,
            TicketPriority.Medium,
            receivedUtc);

        dbContext.Tickets.Add(ticket);

        TicketMessage initialMessage = new(
            Guid.NewGuid(),
            ticket.Id,
            TicketAuthorType.EndUser,
            senderUser.Id,
            body,
            TicketMessageSource.Email,
            request.MessageId,
            receivedUtc);

        dbContext.TicketMessages.Add(initialMessage);
        AddAuditEvent(
            customerId,
            senderUser.Id,
            "ticket.email.created",
            new { ticketId = ticket.Id, messageId = request.MessageId, sender = request.SenderEmail });

        await SaveInboundAttachmentsAsync(ticket.Id, senderUser.Id, request.Attachments, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await aiTicketAgentService.RunForTicketAsync(new AiRunRequest(ticket.Id, "InboundEmailNewTicket"), cancellationToken);
        return new InboundEmailProcessResult(false, true, ticket.Id, null, "New ticket created from inbound email.");
    }

    private async Task<ApplicationUser> EnsureSenderUserAsync(Guid customerId, string senderEmail, CancellationToken cancellationToken)
    {
        ApplicationUser? existing = await userManager.Users
            .SingleOrDefaultAsync(item => item.Email == senderEmail && item.CustomerId == customerId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        ApplicationUser created = new()
        {
            Id = Guid.NewGuid(),
            UserName = senderEmail,
            Email = senderEmail,
            EmailConfirmed = true,
            CustomerId = customerId,
            DisplayName = senderEmail
        };

        string temporaryPassword = $"Tmp!{Guid.NewGuid():N}aA1";
        IdentityResult createResult = await userManager.CreateAsync(created, temporaryPassword);
        if (!createResult.Succeeded)
        {
            string error = string.Join("; ", createResult.Errors.Select(item => item.Description));
            throw new InvalidOperationException($"Failed to create sender user '{senderEmail}'. Errors: {error}");
        }

        IdentityResult roleResult = await userManager.AddToRoleAsync(created, RoleNames.EndUser);
        if (!roleResult.Succeeded)
        {
            string error = string.Join("; ", roleResult.Errors.Select(item => item.Description));
            throw new InvalidOperationException($"Failed to assign EndUser role to '{senderEmail}'. Errors: {error}");
        }

        return created;
    }

    private async Task SaveInboundAttachmentsAsync(
        Guid ticketId,
        Guid senderUserId,
        IReadOnlyList<InboundEmailAttachmentRequest>? attachments,
        CancellationToken cancellationToken)
    {
        if (attachments is null || attachments.Count == 0)
        {
            return;
        }

        foreach (InboundEmailAttachmentRequest attachment in attachments)
        {
            byte[] bytes = Convert.FromBase64String(attachment.Base64Content);
            await using MemoryStream stream = new(bytes, writable: false);

            AttachmentUploadRequest upload = new(attachment.FileName, attachment.ContentType, bytes.LongLength, stream);
            string storagePath = await attachmentStorage.SaveAsync(ticketId, upload, cancellationToken);

            TicketAttachment entity = new(
                Guid.NewGuid(),
                ticketId,
                attachment.FileName,
                attachment.ContentType,
                bytes.LongLength,
                storagePath,
                senderUserId,
                DateTime.UtcNow);

            dbContext.TicketAttachments.Add(entity);
        }
    }

    private static string GetBodyText(InboundEmailRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.PlainTextBody))
        {
            return request.PlainTextBody.Trim();
        }

        if (string.IsNullOrWhiteSpace(request.HtmlBody))
        {
            return "No body content supplied.";
        }

        string withoutTags = Regex.Replace(request.HtmlBody, "<.*?>", " ");
        return Regex.Replace(withoutTags, "\\s+", " ").Trim();
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
