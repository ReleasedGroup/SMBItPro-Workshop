namespace Helpdesk.Light.Domain.Tickets;

public sealed class TicketAttachment
{
    private TicketAttachment()
    {
        FileName = string.Empty;
        ContentType = string.Empty;
        StoragePath = string.Empty;
    }

    public TicketAttachment(
        Guid id,
        Guid ticketId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storagePath,
        Guid? uploadedByUserId,
        DateTime createdUtc)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("Ticket id is required.", nameof(ticketId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        if (sizeBytes < 0)
        {
            throw new ArgumentException("Attachment size cannot be negative.", nameof(sizeBytes));
        }

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        TicketId = ticketId;
        FileName = fileName.Trim();
        ContentType = contentType.Trim();
        SizeBytes = sizeBytes;
        StoragePath = storagePath.Trim();
        UploadedByUserId = uploadedByUserId;
        CreatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    public long SizeBytes { get; private set; }

    public string StoragePath { get; private set; }

    public Guid? UploadedByUserId { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public Ticket? Ticket { get; private set; }
}
