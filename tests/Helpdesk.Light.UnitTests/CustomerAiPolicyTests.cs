using Helpdesk.Light.Domain.Ai;
using Helpdesk.Light.Domain.Entities;

namespace Helpdesk.Light.UnitTests;

public sealed class CustomerAiPolicyTests
{
    [Fact]
    public void SetAiPolicy_ValidRange_UpdatesValues()
    {
        Customer customer = new(Guid.NewGuid(), "Contoso");

        customer.SetAiPolicy(AiPolicyMode.AutoRespondLowRisk, 0.9);

        Assert.Equal(AiPolicyMode.AutoRespondLowRisk, customer.AiPolicyMode);
        Assert.Equal(0.9, customer.AutoRespondMinConfidence);
    }

    [Fact]
    public void SetAiPolicy_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        Customer customer = new(Guid.NewGuid(), "Contoso");

        Assert.Throws<ArgumentOutOfRangeException>(() => customer.SetAiPolicy(AiPolicyMode.SuggestOnly, 1.2));
    }
}
