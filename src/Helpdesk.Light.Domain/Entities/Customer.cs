namespace Helpdesk.Light.Domain.Entities;

public sealed class Customer
{
    private readonly List<CustomerDomain> domains = [];

    private Customer()
    {
        Name = string.Empty;
    }

    public Customer(Guid id, string name, bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = name.Trim();
        IsActive = isActive;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<CustomerDomain> Domains => domains;

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    public CustomerDomain AddDomain(Guid domainId, string domain, bool isPrimary)
    {
        string normalized = CustomerDomain.NormalizeDomain(domain);
        bool exists = domains.Any(item => item.Domain.Equals(normalized, StringComparison.Ordinal));

        if (exists)
        {
            throw new InvalidOperationException($"Domain '{normalized}' is already mapped for customer '{Id}'.");
        }

        if (isPrimary)
        {
            foreach (CustomerDomain item in domains)
            {
                item.SetPrimary(false);
            }
        }

        CustomerDomain added = new(domainId, Id, normalized, isPrimary);
        domains.Add(added);

        return added;
    }
}
