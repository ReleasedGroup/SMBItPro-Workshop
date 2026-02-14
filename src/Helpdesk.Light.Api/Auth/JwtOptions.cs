namespace Helpdesk.Light.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "Helpdesk-Light";

    public string Audience { get; init; } = "Helpdesk-Light-Clients";

    public string SigningKey { get; init; } = "change-me-in-production-with-at-least-32-characters";

    public int ExpiryMinutes { get; init; } = 60;
}
