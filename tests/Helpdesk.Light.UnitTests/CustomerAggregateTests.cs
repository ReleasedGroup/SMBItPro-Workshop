using Helpdesk.Light.Domain.Entities;

namespace Helpdesk.Light.UnitTests;

public sealed class CustomerAggregateTests
{
    [Fact]
    public void AddDomain_WhenPrimary_ResetsOtherPrimaryFlags()
    {
        Customer customer = new(Guid.NewGuid(), "Contoso");
        CustomerDomain first = customer.AddDomain(Guid.NewGuid(), "contoso.com", true);

        CustomerDomain second = customer.AddDomain(Guid.NewGuid(), "help.contoso.com", true);

        Assert.False(first.IsPrimary);
        Assert.True(second.IsPrimary);
    }

    [Fact]
    public void AddDomain_WhenDuplicate_ThrowsInvalidOperationException()
    {
        Customer customer = new(Guid.NewGuid(), "Contoso");
        customer.AddDomain(Guid.NewGuid(), "contoso.com", true);

        Assert.Throws<InvalidOperationException>(() => customer.AddDomain(Guid.NewGuid(), "CONTOSO.COM", false));
    }
}
