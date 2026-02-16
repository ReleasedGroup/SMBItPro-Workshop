using System.Globalization;
using System.Text.Json;
using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Errors;
using Helpdesk.Light.Domain.Entities;
using Helpdesk.Light.Domain.Security;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;
using Helpdesk.Light.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class PlatformSettingsService(
    HelpdeskDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    IOptions<AiOptions> aiOptions,
    IOptions<EmailOptions> emailOptions) : IPlatformSettingsService
{
    private const string EnableAiKey = "ai.enable";
    private const string OpenAiApiKeyKey = "ai.openai_api_key";

    private const string EmailTransportModeKey = "email.transport_mode";

    private const string SmtpHostKey = "email.smtp.host";
    private const string SmtpPortKey = "email.smtp.port";
    private const string SmtpUseSslKey = "email.smtp.use_ssl";
    private const string SmtpUsernameKey = "email.smtp.username";
    private const string SmtpPasswordKey = "email.smtp.password";
    private const string SmtpFromAddressKey = "email.smtp.from_address";
    private const string SmtpFromDisplayNameKey = "email.smtp.from_display_name";

    private const string GraphTenantIdKey = "email.graph.tenant_id";
    private const string GraphClientIdKey = "email.graph.client_id";
    private const string GraphClientSecretKey = "email.graph.client_secret";
    private const string GraphSenderUserIdKey = "email.graph.sender_user_id";

    private readonly AiOptions aiDefaults = aiOptions.Value;
    private readonly EmailOptions emailDefaults = emailOptions.Value;

    public async Task<PlatformSettingsDto> GetAdminSettingsAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();
        RuntimePlatformSettings runtime = await GetRuntimeSettingsAsync(cancellationToken);

        return new PlatformSettingsDto(
            runtime.EnableAi,
            runtime.ModelId,
            !string.IsNullOrWhiteSpace(runtime.OpenAIApiKey),
            runtime.EmailTransportMode,
            new SmtpSettingsDto(
                runtime.Smtp.Host,
                runtime.Smtp.Port,
                runtime.Smtp.UseSsl,
                runtime.Smtp.Username,
                runtime.Smtp.FromAddress,
                runtime.Smtp.FromDisplayName,
                !string.IsNullOrWhiteSpace(runtime.Smtp.Password)),
            new GraphEmailSettingsDto(
                runtime.Graph.TenantId,
                runtime.Graph.ClientId,
                runtime.Graph.SenderUserId,
                !string.IsNullOrWhiteSpace(runtime.Graph.ClientSecret)));
    }

    public async Task<PlatformSettingsDto> UpdateAdminSettingsAsync(PlatformSettingsUpdateRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdminAccess();
        ArgumentNullException.ThrowIfNull(request);

        if (request.Smtp.Port is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "SMTP port must be between 1 and 65535.");
        }

        string normalizedTransport = NormalizeTransportMode(request.EmailTransportMode);
        string smtpHost = (request.Smtp.Host ?? string.Empty).Trim();
        string smtpUsername = (request.Smtp.Username ?? string.Empty).Trim();
        string smtpFromAddress = (request.Smtp.FromAddress ?? string.Empty).Trim();
        string smtpFromDisplayName = (request.Smtp.FromDisplayName ?? string.Empty).Trim();
        string graphTenantId = (request.Graph.TenantId ?? string.Empty).Trim();
        string graphClientId = (request.Graph.ClientId ?? string.Empty).Trim();
        string graphSenderUserId = (request.Graph.SenderUserId ?? string.Empty).Trim();
        DateTime utcNow = DateTime.UtcNow;

        List<PlatformSetting> settings = await dbContext.PlatformSettings.ToListAsync(cancellationToken);

        Upsert(settings, EnableAiKey, request.EnableAi.ToString(CultureInfo.InvariantCulture), utcNow);
        Upsert(settings, EmailTransportModeKey, normalizedTransport, utcNow);

        Upsert(settings, SmtpHostKey, smtpHost, utcNow);
        Upsert(settings, SmtpPortKey, request.Smtp.Port.ToString(CultureInfo.InvariantCulture), utcNow);
        Upsert(settings, SmtpUseSslKey, request.Smtp.UseSsl.ToString(CultureInfo.InvariantCulture), utcNow);
        Upsert(settings, SmtpUsernameKey, smtpUsername, utcNow);
        Upsert(settings, SmtpFromAddressKey, smtpFromAddress, utcNow);
        Upsert(settings, SmtpFromDisplayNameKey, smtpFromDisplayName, utcNow);

        Upsert(settings, GraphTenantIdKey, graphTenantId, utcNow);
        Upsert(settings, GraphClientIdKey, graphClientId, utcNow);
        Upsert(settings, GraphSenderUserIdKey, graphSenderUserId, utcNow);

        ApplySecretUpdate(settings, OpenAiApiKeyKey, request.OpenAIApiKey, request.ClearOpenAIApiKey, utcNow);
        ApplySecretUpdate(settings, SmtpPasswordKey, request.Smtp.Password, request.Smtp.ClearPassword, utcNow);
        ApplySecretUpdate(settings, GraphClientSecretKey, request.Graph.ClientSecret, request.Graph.ClearClientSecret, utcNow);

        AddAuditEvent(
            tenantContextAccessor.Current.UserId,
            "platform.settings.updated",
            new
            {
                request.EnableAi,
                EmailTransportMode = normalizedTransport,
                SmtpHost = smtpHost,
                request.Smtp.Port,
                request.Smtp.UseSsl,
                SmtpUsername = smtpUsername,
                SmtpFromAddress = smtpFromAddress,
                SmtpFromDisplayName = smtpFromDisplayName,
                GraphTenantId = graphTenantId,
                GraphClientId = graphClientId,
                GraphSenderUserId = graphSenderUserId,
                UpdatedOpenAiApiKey = !string.IsNullOrWhiteSpace(request.OpenAIApiKey) || request.ClearOpenAIApiKey,
                UpdatedSmtpPassword = !string.IsNullOrWhiteSpace(request.Smtp.Password) || request.Smtp.ClearPassword,
                UpdatedGraphClientSecret = !string.IsNullOrWhiteSpace(request.Graph.ClientSecret) || request.Graph.ClearClientSecret
            });

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAdminSettingsAsync(cancellationToken);
    }

    public async Task<RuntimePlatformSettings> GetRuntimeSettingsAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<string, string> values = await dbContext.PlatformSettings
            .AsNoTracking()
            .ToDictionaryAsync(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase, cancellationToken);

        bool enableAi = ParseBool(values, EnableAiKey, aiDefaults.EnableAi);
        string? openAiApiKey = ParseString(values, OpenAiApiKeyKey, aiDefaults.OpenAIApiKey);

        string transportMode = NormalizeTransportMode(ParseString(values, EmailTransportModeKey, emailDefaults.TransportMode));

        RuntimeSmtpSettings smtp = new(
            ParseString(values, SmtpHostKey, emailDefaults.SmtpHost),
            ParseInt(values, SmtpPortKey, emailDefaults.SmtpPort),
            ParseBool(values, SmtpUseSslKey, emailDefaults.SmtpUseSsl),
            ParseString(values, SmtpUsernameKey, emailDefaults.SmtpUsername),
            ParseString(values, SmtpPasswordKey, emailDefaults.SmtpPassword),
            ParseString(values, SmtpFromAddressKey, emailDefaults.SmtpFromAddress),
            ParseString(values, SmtpFromDisplayNameKey, emailDefaults.SmtpFromDisplayName));

        RuntimeGraphEmailSettings graph = new(
            ParseString(values, GraphTenantIdKey, emailDefaults.GraphTenantId),
            ParseString(values, GraphClientIdKey, emailDefaults.GraphClientId),
            ParseString(values, GraphClientSecretKey, emailDefaults.GraphClientSecret),
            ParseString(values, GraphSenderUserIdKey, emailDefaults.GraphSenderUserId));

        return new RuntimePlatformSettings(
            enableAi,
            aiDefaults.ModelId,
            openAiApiKey,
            transportMode,
            smtp,
            graph);
    }

    private void EnsureAdminAccess()
    {
        TenantAccessContext context = tenantContextAccessor.Current;
        if (!context.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        if (context.Role != RoleNames.MspAdmin)
        {
            throw new TenantAccessDeniedException("MSP admin role is required for platform settings.");
        }
    }

    private void AddAuditEvent(Guid? actorUserId, string eventType, object payload)
    {
        dbContext.AuditEvents.Add(new AuditEvent(
            Guid.NewGuid(),
            null,
            actorUserId,
            eventType,
            JsonSerializer.Serialize(payload),
            DateTime.UtcNow));
    }

    private void Upsert(List<PlatformSetting> settings, string key, string value, DateTime utcNow)
    {
        PlatformSetting? existing = settings.FirstOrDefault(item => item.Key == key);
        if (existing is null)
        {
            PlatformSetting created = new(key, value, utcNow);
            dbContext.PlatformSettings.Add(created);
            settings.Add(created);
            return;
        }

        existing.UpdateValue(value, utcNow);
    }

    private void ApplySecretUpdate(
        List<PlatformSetting> settings,
        string key,
        string? value,
        bool clear,
        DateTime utcNow)
    {
        if (clear)
        {
            Upsert(settings, key, string.Empty, utcNow);
            return;
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            Upsert(settings, key, value.Trim(), utcNow);
        }
    }

    private static string ParseString(Dictionary<string, string> values, string key, string? defaultValue)
    {
        return values.TryGetValue(key, out string? value) ? value : defaultValue ?? string.Empty;
    }

    private static bool ParseBool(Dictionary<string, string> values, string key, bool defaultValue)
    {
        if (!values.TryGetValue(key, out string? value))
        {
            return defaultValue;
        }

        return bool.TryParse(value, out bool parsed) ? parsed : defaultValue;
    }

    private static int ParseInt(Dictionary<string, string> values, string key, int defaultValue)
    {
        if (!values.TryGetValue(key, out string? value))
        {
            return defaultValue;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultValue;
    }

    public static string NormalizeTransportMode(string? mode)
    {
        if (mode?.Equals(EmailTransportModes.Smtp, StringComparison.OrdinalIgnoreCase) == true)
        {
            return EmailTransportModes.Smtp;
        }

        if (mode?.Equals(EmailTransportModes.Graph, StringComparison.OrdinalIgnoreCase) == true)
        {
            return EmailTransportModes.Graph;
        }

        return EmailTransportModes.Console;
    }
}
