namespace Helpdesk.Light.Domain.Tickets;

public sealed class Ticket
{
    private static readonly IReadOnlyDictionary<TicketStatus, TicketStatus[]> AllowedTransitions = new Dictionary<TicketStatus, TicketStatus[]>
    {
        [TicketStatus.New] = [TicketStatus.Triaged, TicketStatus.InProgress, TicketStatus.WaitingCustomer, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.Triaged] = [TicketStatus.InProgress, TicketStatus.WaitingCustomer, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.InProgress] = [TicketStatus.WaitingCustomer, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.WaitingCustomer] = [TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.InProgress],
        [TicketStatus.Closed] = [TicketStatus.InProgress]
    };

    private Ticket()
    {
        Subject = string.Empty;
        Summary = string.Empty;
        ReferenceCode = string.Empty;
    }

    public Ticket(
        Guid id,
        Guid customerId,
        Guid createdByUserId,
        TicketChannel channel,
        string subject,
        string summary,
        TicketPriority priority,
        DateTime createdUtc)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Creator user id is required.", nameof(createdByUserId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CustomerId = customerId;
        CreatedByUserId = createdByUserId;
        Channel = channel;
        Status = TicketStatus.New;
        Priority = priority;
        Subject = subject.Trim();
        Summary = summary.Trim();
        CreatedUtc = createdUtc;
        UpdatedUtc = createdUtc;
        ReferenceCode = BuildReferenceCode(Id);
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public TicketChannel Channel { get; private set; }

    public TicketStatus Status { get; private set; }

    public TicketPriority Priority { get; private set; }

    public string? Category { get; private set; }

    public string Subject { get; private set; }

    public string Summary { get; private set; }

    public Guid? AssignedToUserId { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime UpdatedUtc { get; private set; }

    public DateTime? ResolvedUtc { get; private set; }

    public string ReferenceCode { get; private set; }

    public void Assign(Guid? assignedToUserId, DateTime utcNow)
    {
        AssignedToUserId = assignedToUserId;
        UpdatedUtc = utcNow;
    }

    public void SetPriority(TicketPriority priority, DateTime utcNow)
    {
        Priority = priority;
        UpdatedUtc = utcNow;
    }

    public void SetCategory(string? category, DateTime utcNow)
    {
        Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        UpdatedUtc = utcNow;
    }

    public void SetSummary(string summary, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        Summary = summary.Trim();
        UpdatedUtc = utcNow;
    }

    public void TransitionStatus(TicketStatus nextStatus, DateTime utcNow)
    {
        if (nextStatus == Status)
        {
            return;
        }

        if (!AllowedTransitions.TryGetValue(Status, out TicketStatus[]? next) || !next.Contains(nextStatus))
        {
            throw new InvalidOperationException($"Cannot transition from {Status} to {nextStatus}.");
        }

        Status = nextStatus;
        UpdatedUtc = utcNow;

        if (nextStatus == TicketStatus.Resolved)
        {
            ResolvedUtc = utcNow;
        }
        else if (nextStatus == TicketStatus.InProgress)
        {
            ResolvedUtc = null;
        }
    }

    public static string BuildReferenceCode(Guid ticketId)
    {
        return $"HD-{ticketId:N}".ToUpperInvariant();
    }
}
