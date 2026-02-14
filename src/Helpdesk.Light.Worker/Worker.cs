using Helpdesk.Light.Application.Abstractions.Email;
using Microsoft.Extensions.DependencyInjection;

namespace Helpdesk.Light.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                IOutboundEmailService outboundEmailService = scope.ServiceProvider.GetRequiredService<IOutboundEmailService>();
                await outboundEmailService.DispatchPendingAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbound dispatch cycle failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
