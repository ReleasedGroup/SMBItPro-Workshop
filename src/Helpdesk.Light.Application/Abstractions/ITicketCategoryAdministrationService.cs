using Helpdesk.Light.Application.Contracts;

namespace Helpdesk.Light.Application.Abstractions;

public interface ITicketCategoryAdministrationService
{
    Task<IReadOnlyList<TicketCategorySummaryDto>> ListTicketCategoriesAsync(Guid? customerId, CancellationToken cancellationToken = default);

    Task<TicketCategorySummaryDto> CreateTicketCategoryAsync(CreateTicketCategoryRequest request, CancellationToken cancellationToken = default);

    Task<TicketCategorySummaryDto> UpdateTicketCategoryAsync(Guid ticketCategoryId, UpdateTicketCategoryRequest request, CancellationToken cancellationToken = default);

    Task DeleteTicketCategoryAsync(Guid ticketCategoryId, CancellationToken cancellationToken = default);
}
