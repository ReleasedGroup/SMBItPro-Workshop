using Helpdesk.Light.Domain.Ai;

namespace Helpdesk.Light.UnitTests;

public sealed class KnowledgeArticleLifecycleTests
{
    [Fact]
    public void Draft_UpdateAndPublishArchiveFlow_Succeeds()
    {
        DateTime createdUtc = DateTime.UtcNow;

        KnowledgeArticle article = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Original title",
            "Original content",
            aiGenerated: true,
            editedByUserId: Guid.NewGuid(),
            createdUtc);

        article.UpdateDraft("Updated title", "Updated content", Guid.NewGuid(), createdUtc.AddMinutes(5));

        Assert.Equal(KnowledgeArticleStatus.Draft, article.Status);
        Assert.Equal(2, article.Version);

        article.Publish(Guid.NewGuid(), createdUtc.AddMinutes(10));

        Assert.Equal(KnowledgeArticleStatus.Published, article.Status);
        Assert.NotNull(article.PublishedUtc);

        article.Archive(Guid.NewGuid(), createdUtc.AddMinutes(20));

        Assert.Equal(KnowledgeArticleStatus.Archived, article.Status);
        Assert.NotNull(article.ArchivedUtc);
    }

    [Fact]
    public void Publish_NonDraft_Throws()
    {
        KnowledgeArticle article = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Title",
            "Content",
            aiGenerated: false,
            editedByUserId: null,
            DateTime.UtcNow);

        article.Publish(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => article.Publish(Guid.NewGuid(), DateTime.UtcNow));
    }
}
