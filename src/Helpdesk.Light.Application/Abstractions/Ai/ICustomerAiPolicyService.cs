using Helpdesk.Light.Application.Contracts.Ai;

namespace Helpdesk.Light.Application.Abstractions.Ai;

public interface ICustomerAiPolicyService
{
    Task UpdatePolicyAsync(Guid customerId, CustomerAiPolicyUpdateRequest request, CancellationToken cancellationToken = default);
}
