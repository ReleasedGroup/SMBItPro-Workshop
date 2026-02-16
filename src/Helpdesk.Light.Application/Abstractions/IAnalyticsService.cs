using Helpdesk.Light.Application.Contracts;

namespace Helpdesk.Light.Application.Abstractions;

public interface IAnalyticsService
{
    Task<AnalyticsDashboardDto> GetDashboardAsync(AnalyticsDashboardRequest request, CancellationToken cancellationToken = default);
}
