using Helpdesk.Light.Infrastructure.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Helpdesk.Light.Infrastructure.Health;

public sealed class AiProviderHealthCheck(IOptions<AiOptions> options) : IHealthCheck
{
    private readonly AiOptions aiOptions = options.Value;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!aiOptions.EnableAi)
        {
            return Task.FromResult(HealthCheckResult.Healthy("AI provider is disabled by configuration."));
        }

        if (string.IsNullOrWhiteSpace(aiOptions.OpenAIApiKey))
        {
            return Task.FromResult(HealthCheckResult.Degraded("AI provider key is missing; fallback generation is active."));
        }

        return Task.FromResult(HealthCheckResult.Healthy($"AI provider configured with model '{aiOptions.ModelId}'."));
    }
}
