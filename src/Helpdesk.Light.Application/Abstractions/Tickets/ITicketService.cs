using Helpdesk.Light.Application.Contracts.Tickets;

namespace Helpdesk.Light.Application.Abstractions.Tickets;

public interface ITicketService
{
    Task<TicketSummaryDto> CreateTicketAsync(CreateTicketRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TicketSummaryDto>> ListTicketsAsync(TicketFilterRequest request, CancellationToken cancellationToken = default);

    Task<TicketDetailDto?> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<TicketMessageDto> AddMessageAsync(Guid ticketId, TicketMessageCreateRequest request, CancellationToken cancellationToken = default);

    Task<TicketSummaryDto> AssignTicketAsync(Guid ticketId, TicketAssignRequest request, CancellationToken cancellationToken = default);

    Task<TicketSummaryDto> UpdateStatusAsync(Guid ticketId, TicketStatusUpdateRequest request, CancellationToken cancellationToken = default);

    Task<TicketSummaryDto> UpdateTriageAsync(Guid ticketId, TicketTriageUpdateRequest request, CancellationToken cancellationToken = default);

    Task<TicketAttachmentDto> UploadAttachmentAsync(Guid ticketId, AttachmentUploadRequest request, CancellationToken cancellationToken = default);

    Task<AttachmentDownloadResult?> DownloadAttachmentAsync(Guid ticketId, Guid attachmentId, CancellationToken cancellationToken = default);
}
