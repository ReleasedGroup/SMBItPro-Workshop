namespace Helpdesk.Light.Domain.Entities;

public sealed class CustomerDomain
{
    private CustomerDomain()
    {
        Domain = string.Empty;
    }

    public CustomerDomain(Guid id, Guid customerId, string domain, bool isPrimary)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id must be set.", nameof(customerId));
        }

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CustomerId = customerId;
        Domain = NormalizeDomain(domain);
        IsPrimary = isPrimary;
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public string Domain { get; private set; }

    public bool IsPrimary { get; private set; }

    public Customer? Customer { get; private set; }

    public void SetPrimary(bool isPrimary)
    {
        IsPrimary = isPrimary;
    }

    public static string NormalizeDomain(string domain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        string normalized = domain.Trim().ToLowerInvariant();

        if (normalized.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("Domain should not include '@'.", nameof(domain));
        }

        return normalized;
    }

    public static string ExtractDomainFromEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        int atIndex = email.LastIndexOf('@');
        if (atIndex <= 0 || atIndex >= email.Length - 1)
        {
            throw new ArgumentException("Email must contain a valid domain.", nameof(email));
        }

        return NormalizeDomain(email[(atIndex + 1)..]);
    }
}
