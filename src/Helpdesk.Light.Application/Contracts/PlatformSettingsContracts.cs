namespace Helpdesk.Light.Application.Contracts;

public static class EmailTransportModes
{
    public const string Console = "Console";
    public const string Smtp = "Smtp";
    public const string Graph = "Graph";
}

public sealed record SmtpSettingsDto(
    string Host,
    int Port,
    bool UseSsl,
    string Username,
    string FromAddress,
    string FromDisplayName,
    bool HasPassword);

public sealed record GraphEmailSettingsDto(
    string TenantId,
    string ClientId,
    string SenderUserId,
    bool HasClientSecret);

public sealed record PlatformSettingsDto(
    bool EnableAi,
    string ModelId,
    bool HasOpenAIApiKey,
    string EmailTransportMode,
    SmtpSettingsDto Smtp,
    GraphEmailSettingsDto Graph);

public sealed record SmtpSettingsUpdateRequest(
    string Host,
    int Port,
    bool UseSsl,
    string Username,
    string FromAddress,
    string FromDisplayName,
    string? Password,
    bool ClearPassword);

public sealed record GraphEmailSettingsUpdateRequest(
    string TenantId,
    string ClientId,
    string SenderUserId,
    string? ClientSecret,
    bool ClearClientSecret);

public sealed record PlatformSettingsUpdateRequest(
    bool EnableAi,
    string EmailTransportMode,
    string? OpenAIApiKey,
    bool ClearOpenAIApiKey,
    SmtpSettingsUpdateRequest Smtp,
    GraphEmailSettingsUpdateRequest Graph);

public sealed record RuntimeSmtpSettings(
    string Host,
    int Port,
    bool UseSsl,
    string Username,
    string Password,
    string FromAddress,
    string FromDisplayName);

public sealed record RuntimeGraphEmailSettings(
    string TenantId,
    string ClientId,
    string ClientSecret,
    string SenderUserId);

public sealed record RuntimePlatformSettings(
    bool EnableAi,
    string ModelId,
    string? OpenAIApiKey,
    string EmailTransportMode,
    RuntimeSmtpSettings Smtp,
    RuntimeGraphEmailSettings Graph);
