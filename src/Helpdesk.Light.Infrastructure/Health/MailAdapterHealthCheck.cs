using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Helpdesk.Light.Infrastructure.Health;

public sealed class MailAdapterHealthCheck(IPlatformSettingsService platformSettingsService) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        RuntimePlatformSettings settings = await platformSettingsService.GetRuntimeSettingsAsync(cancellationToken);

        if (settings.EmailTransportMode == EmailTransportModes.Smtp)
        {
            bool valid = !string.IsNullOrWhiteSpace(settings.Smtp.Host) &&
                         !string.IsNullOrWhiteSpace(settings.Smtp.FromAddress);
            return valid
                ? HealthCheckResult.Healthy("SMTP adapter is configured.")
                : HealthCheckResult.Degraded("SMTP adapter is selected but host/from address are missing.");
        }

        if (settings.EmailTransportMode == EmailTransportModes.Graph)
        {
            bool valid = !string.IsNullOrWhiteSpace(settings.Graph.TenantId) &&
                         !string.IsNullOrWhiteSpace(settings.Graph.ClientId) &&
                         !string.IsNullOrWhiteSpace(settings.Graph.ClientSecret) &&
                         !string.IsNullOrWhiteSpace(settings.Graph.SenderUserId);
            return valid
                ? HealthCheckResult.Healthy("Microsoft Graph adapter is configured.")
                : HealthCheckResult.Degraded("Graph adapter is selected but tenant/client/sender credentials are incomplete.");
        }

        return HealthCheckResult.Healthy("Console email adapter is configured.");
    }
}
