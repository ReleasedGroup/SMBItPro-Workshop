namespace Helpdesk.Light.Domain.Ai;

public sealed class KnowledgeArticle
{
    private KnowledgeArticle()
    {
        Title = string.Empty;
        ContentMarkdown = string.Empty;
    }

    public KnowledgeArticle(
        Guid id,
        Guid? customerId,
        Guid? sourceTicketId,
        string title,
        string contentMarkdown,
        bool aiGenerated,
        Guid? editedByUserId,
        DateTime createdUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentMarkdown);

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CustomerId = customerId;
        SourceTicketId = sourceTicketId;
        Title = title.Trim();
        ContentMarkdown = contentMarkdown;
        Status = KnowledgeArticleStatus.Draft;
        Version = 1;
        AiGenerated = aiGenerated;
        LastEditedByUserId = editedByUserId;
        CreatedUtc = createdUtc;
        UpdatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }

    public Guid? CustomerId { get; private set; }

    public Guid? SourceTicketId { get; private set; }

    public string Title { get; private set; }

    public string ContentMarkdown { get; private set; }

    public KnowledgeArticleStatus Status { get; private set; }

    public int Version { get; private set; }

    public bool AiGenerated { get; private set; }

    public Guid? LastEditedByUserId { get; private set; }

    public Guid? PublishedByUserId { get; private set; }

    public Guid? ArchivedByUserId { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime UpdatedUtc { get; private set; }

    public DateTime? PublishedUtc { get; private set; }

    public DateTime? ArchivedUtc { get; private set; }

    public void UpdateDraft(
        string title,
        string contentMarkdown,
        Guid? editedByUserId,
        DateTime utcNow)
    {
        if (Status != KnowledgeArticleStatus.Draft)
        {
            throw new InvalidOperationException("Only draft articles can be edited.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentMarkdown);

        bool hasChanges =
            !string.Equals(Title, title.Trim(), StringComparison.Ordinal) ||
            !string.Equals(ContentMarkdown, contentMarkdown, StringComparison.Ordinal);

        Title = title.Trim();
        ContentMarkdown = contentMarkdown;
        LastEditedByUserId = editedByUserId;
        UpdatedUtc = utcNow;

        if (hasChanges)
        {
            Version += 1;
        }
    }

    public void Publish(Guid? publishedByUserId, DateTime utcNow)
    {
        if (Status != KnowledgeArticleStatus.Draft)
        {
            throw new InvalidOperationException("Only draft articles can be published.");
        }

        Status = KnowledgeArticleStatus.Published;
        PublishedByUserId = publishedByUserId;
        PublishedUtc = utcNow;
        UpdatedUtc = utcNow;
    }

    public void Archive(Guid? archivedByUserId, DateTime utcNow)
    {
        if (Status != KnowledgeArticleStatus.Published)
        {
            throw new InvalidOperationException("Only published articles can be archived.");
        }

        Status = KnowledgeArticleStatus.Archived;
        ArchivedByUserId = archivedByUserId;
        ArchivedUtc = utcNow;
        UpdatedUtc = utcNow;
    }
}
