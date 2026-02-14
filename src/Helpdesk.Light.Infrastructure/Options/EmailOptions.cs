namespace Helpdesk.Light.Infrastructure.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public int MaxRetryCount { get; init; } = 3;
}
