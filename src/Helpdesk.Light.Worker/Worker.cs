using Helpdesk.Light.Application.Abstractions.Email;

namespace Helpdesk.Light.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    IOutboundEmailService outboundEmailService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await outboundEmailService.DispatchPendingAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbound dispatch cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
