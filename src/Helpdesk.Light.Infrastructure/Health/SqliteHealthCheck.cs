using Helpdesk.Light.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Helpdesk.Light.Infrastructure.Health;

public sealed class SqliteHealthCheck(HelpdeskDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        bool canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy("SQLite connection is healthy.")
            : HealthCheckResult.Unhealthy("SQLite connection check failed.");
    }
}
