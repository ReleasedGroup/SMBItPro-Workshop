using System.Text.Json;
using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Abstractions.Email;
using Helpdesk.Light.Application.Contracts.Email;
using Helpdesk.Light.Domain.Email;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;
using Helpdesk.Light.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class OutboundEmailService(
    HelpdeskDbContext dbContext,
    IEmailTransport emailTransport,
    ITenantContextAccessor tenantContextAccessor,
    IRuntimeMetricsRecorder runtimeMetrics,
    IOptions<EmailOptions> options) : IOutboundEmailService
{
    private readonly EmailOptions emailOptions = options.Value;

    public async Task QueueAsync(Guid customerId, Guid? ticketId, string toAddress, string subject, string body, string correlationKey, CancellationToken cancellationToken = default)
    {
        bool exists = await dbContext.OutboundEmailMessages
            .AnyAsync(item => item.CorrelationKey == correlationKey && item.Status == OutboundEmailStatus.Sent, cancellationToken);

        if (exists)
        {
            return;
        }

        OutboundEmailMessage message = new(Guid.NewGuid(), ticketId, customerId, toAddress, subject, body, correlationKey, DateTime.UtcNow);
        dbContext.OutboundEmailMessages.Add(message);
        AddAuditEvent(
            customerId,
            tenantContextAccessor.Current.UserId,
            "email.queued",
            new { messageId = message.Id, message.TicketId, message.CorrelationKey, message.ToAddress, message.Subject });

        await dbContext.SaveChangesAsync(cancellationToken);

        await DispatchPendingAsync(cancellationToken);
    }

    public async Task DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        List<OutboundEmailMessage> pending = await dbContext.OutboundEmailMessages
            .Where(item => item.Status == OutboundEmailStatus.Pending || item.Status == OutboundEmailStatus.Failed)
            .OrderBy(item => item.CreatedUtc)
            .ToListAsync(cancellationToken);

        runtimeMetrics.SetWorkerQueueDepth(pending.Count);

        foreach (OutboundEmailMessage message in pending)
        {
            int remainingAttempts = emailOptions.MaxRetryCount - message.AttemptCount;
            if (remainingAttempts <= 0)
            {
                message.MarkDeadLetter($"Exceeded retry limit ({emailOptions.MaxRetryCount}).", DateTime.UtcNow);
                runtimeMetrics.RecordEmailDeadLetter();
                AddAuditEvent(
                    message.CustomerId,
                    tenantContextAccessor.Current.UserId,
                    "email.dead_lettered",
                    new { messageId = message.Id, message.TicketId, message.AttemptCount, reason = message.LastError });
                continue;
            }

            for (int index = 0; index < remainingAttempts; index++)
            {
                message.MarkAttempt();

                try
                {
                    await emailTransport.SendAsync(message.ToAddress, message.Subject, message.Body, cancellationToken);
                    message.MarkSent(DateTime.UtcNow);
                    runtimeMetrics.RecordEmailSent();
                    AddAuditEvent(
                        message.CustomerId,
                        tenantContextAccessor.Current.UserId,
                        "email.sent",
                        new { messageId = message.Id, message.TicketId, message.AttemptCount });
                    break;
                }
                catch (Exception exception)
                {
                    message.MarkFailure(exception.Message);
                    runtimeMetrics.RecordEmailFailed();
                    AddAuditEvent(
                        message.CustomerId,
                        tenantContextAccessor.Current.UserId,
                        "email.send.failed",
                        new { messageId = message.Id, message.TicketId, message.AttemptCount, error = exception.Message });
                }
            }

            if (message.Status != OutboundEmailStatus.Sent && message.AttemptCount >= emailOptions.MaxRetryCount)
            {
                message.MarkDeadLetter($"Exceeded retry limit ({emailOptions.MaxRetryCount}). Last error: {message.LastError}", DateTime.UtcNow);
                runtimeMetrics.RecordEmailDeadLetter();
                AddAuditEvent(
                    message.CustomerId,
                    tenantContextAccessor.Current.UserId,
                    "email.dead_lettered",
                    new { messageId = message.Id, message.TicketId, message.AttemptCount, reason = message.LastError });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        int remaining = await dbContext.OutboundEmailMessages
            .CountAsync(item => item.Status == OutboundEmailStatus.Pending || item.Status == OutboundEmailStatus.Failed, cancellationToken);

        runtimeMetrics.SetWorkerQueueDepth(remaining);
    }

    public async Task<int> RetryDeadLettersAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        int boundedTake = Math.Clamp(take, 1, 500);
        List<OutboundEmailMessage> deadLetters = await dbContext.OutboundEmailMessages
            .Where(item => item.Status == OutboundEmailStatus.DeadLetter)
            .OrderBy(item => item.CreatedUtc)
            .Take(boundedTake)
            .ToListAsync(cancellationToken);

        if (deadLetters.Count == 0)
        {
            return 0;
        }

        foreach (OutboundEmailMessage message in deadLetters)
        {
            message.RetryFromDeadLetter();
            AddAuditEvent(
                message.CustomerId,
                tenantContextAccessor.Current.UserId,
                "email.dead_letter.retry_requested",
                new { messageId = message.Id, message.TicketId, message.AttemptCount });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await DispatchPendingAsync(cancellationToken);
        return deadLetters.Count;
    }

    public async Task<IReadOnlyList<OutboundEmailDto>> ListAsync(Guid? customerId, CancellationToken cancellationToken = default)
    {
        IQueryable<OutboundEmailMessage> query = dbContext.OutboundEmailMessages.AsNoTracking();
        if (customerId.HasValue)
        {
            query = query.Where(item => item.CustomerId == customerId.Value);
        }

        return await query
            .OrderByDescending(item => item.CreatedUtc)
            .Take(200)
            .Select(item => new OutboundEmailDto(
                item.Id,
                item.TicketId,
                item.CustomerId,
                item.ToAddress,
                item.Subject,
                item.Body,
                item.Status.ToString(),
                item.AttemptCount,
                item.LastError,
                item.CreatedUtc,
                item.SentUtc,
                item.DeadLetteredUtc))
            .ToListAsync(cancellationToken);
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
