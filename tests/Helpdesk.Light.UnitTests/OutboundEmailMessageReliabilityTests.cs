using Helpdesk.Light.Domain.Email;

namespace Helpdesk.Light.UnitTests;

public sealed class OutboundEmailMessageReliabilityTests
{
    [Fact]
    public void MarkDeadLetter_ThenRetry_ResetsStateToPending()
    {
        OutboundEmailMessage message = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user@contoso.com",
            "Subject",
            "Body",
            "correlation-key",
            DateTime.UtcNow);

        message.MarkAttempt();
        message.MarkFailure("smtp failure");
        message.MarkDeadLetter("Exceeded retries", DateTime.UtcNow);

        Assert.Equal(OutboundEmailStatus.DeadLetter, message.Status);
        Assert.NotNull(message.DeadLetteredUtc);

        message.RetryFromDeadLetter();

        Assert.Equal(OutboundEmailStatus.Pending, message.Status);
        Assert.Null(message.DeadLetteredUtc);
        Assert.Null(message.LastError);
        Assert.Equal(0, message.AttemptCount);
    }

    [Fact]
    public void RetryFromDeadLetter_NonDeadLetter_Throws()
    {
        OutboundEmailMessage message = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user@contoso.com",
            "Subject",
            "Body",
            "correlation-key",
            DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => message.RetryFromDeadLetter());
    }
}
