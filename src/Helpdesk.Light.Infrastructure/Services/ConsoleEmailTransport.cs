using Helpdesk.Light.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class ConsoleEmailTransport(ILogger<ConsoleEmailTransport> logger) : IEmailTransport
{
    public Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("DEV email transport sent to {ToAddress} with subject {Subject}. Body length: {Length}", toAddress, subject, body.Length);
        return Task.CompletedTask;
    }
}
