using Helpdesk.Light.Domain.Ai;

namespace Helpdesk.Light.Domain.Entities;

public sealed class Customer
{
    private readonly List<CustomerDomain> domains = [];

    private Customer()
    {
        Name = string.Empty;
        AiPolicyMode = AiPolicyMode.SuggestOnly;
        AutoRespondMinConfidence = 0.85;
    }

    public Customer(Guid id, string name, bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = name.Trim();
        IsActive = isActive;
        AiPolicyMode = AiPolicyMode.SuggestOnly;
        AutoRespondMinConfidence = 0.85;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public AiPolicyMode AiPolicyMode { get; private set; }

    public double AutoRespondMinConfidence { get; private set; }

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

    public void SetAiPolicy(AiPolicyMode mode, double autoRespondMinConfidence)
    {
        if (autoRespondMinConfidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(autoRespondMinConfidence), "Confidence threshold must be between 0 and 1.");
        }

        AiPolicyMode = mode;
        AutoRespondMinConfidence = autoRespondMinConfidence;
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
