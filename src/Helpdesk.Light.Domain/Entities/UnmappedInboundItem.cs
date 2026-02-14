namespace Helpdesk.Light.Domain.Entities;

public sealed class UnmappedInboundItem
{
    private UnmappedInboundItem()
    {
        SenderEmail = string.Empty;
        SenderDomain = string.Empty;
        Subject = string.Empty;
    }

    public UnmappedInboundItem(Guid id, string senderEmail, string senderDomain, string? subject, DateTime receivedUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senderEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderDomain);

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        SenderEmail = senderEmail.Trim().ToLowerInvariant();
        SenderDomain = senderDomain.Trim().ToLowerInvariant();
        Subject = subject?.Trim() ?? string.Empty;
        ReceivedUtc = receivedUtc;
    }

    public Guid Id { get; private set; }

    public string SenderEmail { get; private set; }

    public string SenderDomain { get; private set; }

    public string Subject { get; private set; }

    public DateTime ReceivedUtc { get; private set; }
}
