using Helpdesk.Light.Application.Contracts.Email;

namespace Helpdesk.Light.Application.Abstractions.Email;

public interface IInboundEmailService
{
    Task<InboundEmailProcessResult> ProcessAsync(InboundEmailRequest request, CancellationToken cancellationToken = default);
}
