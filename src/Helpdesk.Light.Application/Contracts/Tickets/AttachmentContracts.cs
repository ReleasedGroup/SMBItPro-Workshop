namespace Helpdesk.Light.Application.Contracts.Tickets;

public sealed record AttachmentUploadRequest(string FileName, string ContentType, long SizeBytes, Stream Content);

public sealed record AttachmentDownloadResult(Stream Content, string ContentType, string FileName);
