using Helpdesk.Light.Application.Contracts.Tickets;

namespace Helpdesk.Light.Application.Abstractions.Tickets;

public interface IAttachmentStorage
{
    Task<string> SaveAsync(Guid ticketId, AttachmentUploadRequest request, CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);
}
