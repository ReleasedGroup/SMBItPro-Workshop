using System.Text.Json;
using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Abstractions.Ai;
using Helpdesk.Light.Application.Contracts.Ai;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class CustomerAiPolicyService(
    HelpdeskDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : ICustomerAiPolicyService
{
    public async Task UpdatePolicyAsync(Guid customerId, CustomerAiPolicyUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.SingleOrDefaultAsync(item => item.Id == customerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{customerId}' was not found.");

        customer.SetAiPolicy(request.Mode, request.AutoRespondMinConfidence);
        dbContext.AuditEvents.Add(new AuditEvent(
            Guid.NewGuid(),
            customerId,
            tenantContextAccessor.Current.UserId,
            "admin.customer.ai_policy.updated",
            JsonSerializer.Serialize(new { customerId, request.Mode, request.AutoRespondMinConfidence }),
            DateTime.UtcNow));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
