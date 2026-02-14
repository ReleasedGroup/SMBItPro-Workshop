using Helpdesk.Light.Application.Abstractions.Ai;
using Helpdesk.Light.Application.Contracts.Ai;
using Helpdesk.Light.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class CustomerAiPolicyService(HelpdeskDbContext dbContext) : ICustomerAiPolicyService
{
    public async Task UpdatePolicyAsync(Guid customerId, CustomerAiPolicyUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.SingleOrDefaultAsync(item => item.Id == customerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{customerId}' was not found.");

        customer.SetAiPolicy(request.Mode, request.AutoRespondMinConfidence);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
