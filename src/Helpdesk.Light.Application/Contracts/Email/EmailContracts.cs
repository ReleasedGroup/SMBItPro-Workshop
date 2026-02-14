namespace Helpdesk.Light.Application.Contracts.Email;

public sealed record InboundEmailAttachmentRequest(string FileName, string ContentType, string Base64Content);

public sealed record InboundEmailRequest(
    string MessageId,
    string SenderEmail,
    string Subject,
    string? PlainTextBody,
    string? HtmlBody,
    DateTime? ReceivedUtc,
    IReadOnlyList<InboundEmailAttachmentRequest>? Attachments);

public sealed record InboundEmailProcessResult(bool IsDuplicate, bool IsMapped, Guid? TicketId, Guid? UnmappedQueueItemId, string Detail);

public sealed record OutboundEmailDto(
    Guid Id,
    Guid? TicketId,
    Guid CustomerId,
    string ToAddress,
    string Subject,
    string Body,
    string Status,
    int AttemptCount,
    string? LastError,
    DateTime CreatedUtc,
    DateTime? SentUtc);
