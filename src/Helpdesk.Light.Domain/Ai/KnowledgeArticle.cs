namespace Helpdesk.Light.Domain.Ai;

public sealed class KnowledgeArticle
{
    private KnowledgeArticle()
    {
        Title = string.Empty;
        ContentMarkdown = string.Empty;
        Status = string.Empty;
    }

    public KnowledgeArticle(Guid id, Guid? customerId, string title, string contentMarkdown, string status, DateTime createdUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentMarkdown);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CustomerId = customerId;
        Title = title.Trim();
        ContentMarkdown = contentMarkdown;
        Status = status.Trim();
        CreatedUtc = createdUtc;
        UpdatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }

    public Guid? CustomerId { get; private set; }

    public string Title { get; private set; }

    public string ContentMarkdown { get; private set; }

    public string Status { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime UpdatedUtc { get; private set; }
}
