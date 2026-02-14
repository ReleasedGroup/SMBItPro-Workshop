namespace Helpdesk.Light.Domain.Email;

public sealed class OutboundEmailMessage
{
    private OutboundEmailMessage()
    {
        ToAddress = string.Empty;
        Subject = string.Empty;
        Body = string.Empty;
        CorrelationKey = string.Empty;
    }

    public OutboundEmailMessage(
        Guid id,
        Guid? ticketId,
        Guid customerId,
        string toAddress,
        string subject,
        string body,
        string correlationKey,
        DateTime createdUtc)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(toAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationKey);

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        TicketId = ticketId;
        CustomerId = customerId;
        ToAddress = toAddress.Trim();
        Subject = subject.Trim();
        Body = body;
        CorrelationKey = correlationKey.Trim();
        Status = OutboundEmailStatus.Pending;
        AttemptCount = 0;
        CreatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }

    public Guid? TicketId { get; private set; }

    public Guid CustomerId { get; private set; }

    public string ToAddress { get; private set; }

    public string Subject { get; private set; }

    public string Body { get; private set; }

    public string CorrelationKey { get; private set; }

    public OutboundEmailStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public string? LastError { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime? SentUtc { get; private set; }

    public void MarkSent(DateTime utcNow)
    {
        Status = OutboundEmailStatus.Sent;
        SentUtc = utcNow;
        LastError = null;
    }

    public void MarkFailure(string error)
    {
        LastError = error;
        Status = OutboundEmailStatus.Failed;
    }

    public void MarkAttempt()
    {
        AttemptCount += 1;
    }
}
