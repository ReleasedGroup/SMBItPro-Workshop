using Helpdesk.Light.Application.Abstractions.Tickets;
using Helpdesk.Light.Application.Contracts.Tickets;
using Helpdesk.Light.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class LocalAttachmentStorage(IOptions<AttachmentOptions> options) : IAttachmentStorage
{
    private readonly AttachmentOptions attachmentOptions = options.Value;

    public async Task<string> SaveAsync(Guid ticketId, AttachmentUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SizeBytes <= 0)
        {
            throw new InvalidOperationException("Attachment size must be greater than zero bytes.");
        }

        if (request.SizeBytes > attachmentOptions.MaxSizeBytes)
        {
            throw new InvalidOperationException($"Attachment exceeds maximum size of {attachmentOptions.MaxSizeBytes} bytes.");
        }

        if (attachmentOptions.AllowedContentTypes.Length > 0
            && !attachmentOptions.AllowedContentTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported content type '{request.ContentType}'.");
        }

        string root = Path.GetFullPath(attachmentOptions.RootPath);
        string ticketFolder = Path.Combine(root, ticketId.ToString("N"));
        Directory.CreateDirectory(ticketFolder);

        string safeFileName = SanitizeFileName(request.FileName);
        string storageFileName = $"{Guid.NewGuid():N}_{safeFileName}";
        string physicalPath = Path.Combine(ticketFolder, storageFileName);

        await using FileStream fileStream = File.Create(physicalPath);
        await request.Content.CopyToAsync(fileStream, cancellationToken);

        return physicalPath;
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(storagePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = File.OpenRead(storagePath);
        return Task.FromResult<Stream?>(stream);
    }

    private static string SanitizeFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string name = Path.GetFileName(fileName);
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "file.bin" : name;
    }
}
