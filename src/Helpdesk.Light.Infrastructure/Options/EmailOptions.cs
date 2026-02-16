namespace Helpdesk.Light.Infrastructure.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public int MaxRetryCount { get; init; } = 3;

    public string TransportMode { get; init; } = Helpdesk.Light.Application.Contracts.EmailTransportModes.Console;

    public string SmtpHost { get; init; } = string.Empty;

    public int SmtpPort { get; init; } = 587;

    public bool SmtpUseSsl { get; init; } = true;

    public string SmtpUsername { get; init; } = string.Empty;

    public string SmtpPassword { get; init; } = string.Empty;

    public string SmtpFromAddress { get; init; } = "noreply@localhost";

    public string SmtpFromDisplayName { get; init; } = "Helpdesk Light";

    public string GraphTenantId { get; init; } = string.Empty;

    public string GraphClientId { get; init; } = string.Empty;

    public string GraphClientSecret { get; init; } = string.Empty;

    public string GraphSenderUserId { get; init; } = string.Empty;
}
