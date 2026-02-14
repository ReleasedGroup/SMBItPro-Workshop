namespace Helpdesk.Light.Domain.Tickets;

public sealed class AuditEvent
{
    private AuditEvent()
    {
        EventType = string.Empty;
        PayloadJson = string.Empty;
    }

    public AuditEvent(Guid id, Guid? customerId, Guid? actorUserId, string eventType, string payloadJson, DateTime createdUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CustomerId = customerId;
        ActorUserId = actorUserId;
        EventType = eventType.Trim();
        PayloadJson = payloadJson;
        CreatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }

    public Guid? CustomerId { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string EventType { get; private set; }

    public string PayloadJson { get; private set; }

    public DateTime CreatedUtc { get; private set; }
}
