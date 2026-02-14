using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Domain.Entities;
using Helpdesk.Light.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class TenantResolutionService(HelpdeskDbContext dbContext) : ITenantResolutionService
{
    public async Task<TenantResolutionResultDto> ResolveSenderEmailAsync(ResolveSenderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SenderEmail);

        string domain = CustomerDomain.ExtractDomainFromEmail(request.SenderEmail);

        CustomerDomain? mapped = await dbContext.CustomerDomains
            .AsNoTracking()
            .Include(item => item.Customer)
            .SingleOrDefaultAsync(item => item.Domain == domain, cancellationToken);

        if (mapped is not null && mapped.Customer is not null)
        {
            return new TenantResolutionResultDto(true, mapped.CustomerId, mapped.Customer.Name, domain, null);
        }

        UnmappedInboundItem queueItem = new(Guid.NewGuid(), request.SenderEmail, domain, request.Subject, DateTime.UtcNow);
        dbContext.UnmappedInboundItems.Add(queueItem);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TenantResolutionResultDto(false, null, null, domain, queueItem.Id);
    }

    public async Task<IReadOnlyList<UnmappedInboundItemDto>> ListUnmappedQueueAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.UnmappedInboundItems
            .AsNoTracking()
            .OrderByDescending(item => item.ReceivedUtc)
            .Select(item => new UnmappedInboundItemDto(item.Id, item.SenderEmail, item.SenderDomain, item.Subject, item.ReceivedUtc))
            .ToListAsync(cancellationToken);
    }
}
