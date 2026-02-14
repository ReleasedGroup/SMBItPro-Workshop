using Helpdesk.Light.Application.Contracts.Email;

namespace Helpdesk.Light.Application.Abstractions.Email;

public interface IOutboundEmailService
{
    Task QueueAsync(Guid customerId, Guid? ticketId, string toAddress, string subject, string body, string correlationKey, CancellationToken cancellationToken = default);

    Task DispatchPendingAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboundEmailDto>> ListAsync(Guid? customerId, CancellationToken cancellationToken = default);
}
