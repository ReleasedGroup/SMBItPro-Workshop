namespace Helpdesk.Light.Infrastructure.Options;

public sealed class AttachmentOptions
{
    public const string SectionName = "Attachments";

    public string RootPath { get; init; } = "storage/attachments";

    public long MaxSizeBytes { get; init; } = 10 * 1024 * 1024;

    public string[] AllowedContentTypes { get; init; } =
    [
        "application/pdf",
        "image/png",
        "image/jpeg",
        "text/plain",
        "application/zip"
    ];
}
