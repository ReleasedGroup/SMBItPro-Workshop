using Helpdesk.Light.Domain.Entities;

namespace Helpdesk.Light.UnitTests;

public sealed class CustomerDomainTests
{
    [Fact]
    public void NormalizeDomain_TrimAndLowercase_ReturnsNormalized()
    {
        string normalized = CustomerDomain.NormalizeDomain("  Example.COM  ");

        Assert.Equal("example.com", normalized);
    }

    [Fact]
    public void NormalizeDomain_WithAtSymbol_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CustomerDomain.NormalizeDomain("user@example.com"));
    }

    [Fact]
    public void ExtractDomainFromEmail_WithValidEmail_ReturnsDomain()
    {
        string domain = CustomerDomain.ExtractDomainFromEmail("Tech@Contoso.com");

        Assert.Equal("contoso.com", domain);
    }
}
