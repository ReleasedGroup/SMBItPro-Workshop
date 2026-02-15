using Helpdesk.Light.Application.Abstractions.Email;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Helpdesk.Light.Infrastructure.Health;

public sealed class MailAdapterHealthCheck(IEmailTransport emailTransport) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        string adapter = emailTransport.GetType().Name;
        return Task.FromResult(HealthCheckResult.Healthy($"Mail adapter '{adapter}' is configured."));
    }
}
