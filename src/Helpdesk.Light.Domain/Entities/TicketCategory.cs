namespace Helpdesk.Light.Domain.Entities;

public sealed class TicketCategory
{
    private TicketCategory()
    {
        Name = string.Empty;
    }

    public TicketCategory(
        Guid id,
        Guid customerId,
        string name,
        bool isActive,
        Guid? resolverGroupId,
        DateTime createdUtc)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id must be set.", nameof(customerId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CustomerId = customerId;
        Name = name.Trim();
        IsActive = isActive;
        ResolverGroupId = resolverGroupId;
        CreatedUtc = createdUtc;
        UpdatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public Guid? ResolverGroupId { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime UpdatedUtc { get; private set; }

    public Customer? Customer { get; private set; }

    public ResolverGroup? ResolverGroup { get; private set; }

    public void Rename(string name, DateTime updatedUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        UpdatedUtc = updatedUtc;
    }

    public void SetActive(bool isActive, DateTime updatedUtc)
    {
        IsActive = isActive;
        UpdatedUtc = updatedUtc;
    }

    public void SetResolverGroup(Guid? resolverGroupId, DateTime updatedUtc)
    {
        ResolverGroupId = resolverGroupId;
        UpdatedUtc = updatedUtc;
    }
}
