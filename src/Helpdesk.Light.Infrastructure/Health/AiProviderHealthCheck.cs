using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Helpdesk.Light.Infrastructure.Health;

public sealed class AiProviderHealthCheck(IPlatformSettingsService platformSettingsService) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        RuntimePlatformSettings settings = await platformSettingsService.GetRuntimeSettingsAsync(cancellationToken);
        if (!settings.EnableAi)
        {
            return HealthCheckResult.Healthy("AI provider is disabled by configuration.");
        }

        if (string.IsNullOrWhiteSpace(settings.OpenAIApiKey))
        {
            return HealthCheckResult.Degraded("AI provider key is missing; fallback generation is active.");
        }

        return HealthCheckResult.Healthy($"AI provider configured with model '{settings.ModelId}'.");
    }
}
