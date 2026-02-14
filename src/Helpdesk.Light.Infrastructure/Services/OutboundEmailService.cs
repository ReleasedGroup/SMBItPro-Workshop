using Helpdesk.Light.Application.Abstractions.Email;
using Helpdesk.Light.Application.Contracts.Email;
using Helpdesk.Light.Domain.Email;
using Helpdesk.Light.Infrastructure.Data;
using Helpdesk.Light.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class OutboundEmailService(
    HelpdeskDbContext dbContext,
    IEmailTransport emailTransport,
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
        await dbContext.SaveChangesAsync(cancellationToken);

        await DispatchPendingAsync(cancellationToken);
    }

    public async Task DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        List<OutboundEmailMessage> pending = await dbContext.OutboundEmailMessages
            .Where(item => item.Status != OutboundEmailStatus.Sent && item.AttemptCount < emailOptions.MaxRetryCount)
            .OrderBy(item => item.CreatedUtc)
            .ToListAsync(cancellationToken);

        foreach (OutboundEmailMessage message in pending)
        {
            int remainingAttempts = emailOptions.MaxRetryCount - message.AttemptCount;
            for (int index = 0; index < remainingAttempts; index++)
            {
                message.MarkAttempt();

                try
                {
                    await emailTransport.SendAsync(message.ToAddress, message.Subject, message.Body, cancellationToken);
                    message.MarkSent(DateTime.UtcNow);
                    break;
                }
                catch (Exception exception)
                {
                    message.MarkFailure(exception.Message);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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
                item.SentUtc))
            .ToListAsync(cancellationToken);
    }
}
