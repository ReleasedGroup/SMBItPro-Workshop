namespace Helpdesk.Light.Application.Abstractions.Email;

public interface IEmailTransport
{
    Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default);
}
