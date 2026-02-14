namespace Helpdesk.Light.Domain.Tickets;

public sealed class TicketMessage
{
    private TicketMessage()
    {
        Body = string.Empty;
    }

    public TicketMessage(
        Guid id,
        Guid ticketId,
        TicketAuthorType authorType,
        Guid? authorUserId,
        string body,
        TicketMessageSource source,
        string? externalMessageId,
        DateTime createdUtc)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("Ticket id is required.", nameof(ticketId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        TicketId = ticketId;
        AuthorType = authorType;
        AuthorUserId = authorUserId;
        Body = body.Trim();
        Source = source;
        ExternalMessageId = string.IsNullOrWhiteSpace(externalMessageId) ? null : externalMessageId.Trim();
        CreatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public TicketAuthorType AuthorType { get; private set; }

    public Guid? AuthorUserId { get; private set; }

    public string Body { get; private set; }

    public TicketMessageSource Source { get; private set; }

    public string? ExternalMessageId { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public Ticket? Ticket { get; private set; }
}
