namespace Helpdesk.Light.Application.Contracts;

public sealed record CustomerSummaryDto(Guid Id, string Name, bool IsActive, int DomainCount);

public sealed record CustomerDomainDto(Guid Id, Guid CustomerId, string Domain, bool IsPrimary);

public sealed record CustomerDetailDto(Guid Id, string Name, bool IsActive, IReadOnlyList<CustomerDomainDto> Domains);

public sealed record CreateCustomerRequest(string Name, bool IsActive);

public sealed record AddCustomerDomainRequest(string Domain, bool IsPrimary);

public sealed record ResolveSenderRequest(string SenderEmail, string? Subject);

public sealed record TenantResolutionResultDto(bool IsMapped, Guid? CustomerId, string? CustomerName, string SenderDomain, Guid? UnmappedQueueItemId);

public sealed record UnmappedInboundItemDto(Guid Id, string SenderEmail, string SenderDomain, string Subject, DateTime ReceivedUtc);
