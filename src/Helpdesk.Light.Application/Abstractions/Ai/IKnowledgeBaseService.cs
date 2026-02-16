using Helpdesk.Light.Application.Contracts.Ai;

namespace Helpdesk.Light.Application.Abstractions.Ai;

public interface IKnowledgeBaseService
{
    Task<IReadOnlyList<KnowledgeArticleSummaryDto>> ListAsync(KnowledgeArticleListRequest request, CancellationToken cancellationToken = default);

    Task<KnowledgeArticleDetailDto?> GetAsync(Guid articleId, CancellationToken cancellationToken = default);

    Task<KnowledgeArticleDetailDto> CreateDraftAsync(KnowledgeArticleDraftCreateRequest request, CancellationToken cancellationToken = default);

    Task<KnowledgeArticleDetailDto> UpdateDraftAsync(Guid articleId, KnowledgeArticleDraftUpdateRequest request, CancellationToken cancellationToken = default);

    Task<KnowledgeArticleDetailDto> GenerateDraftFromTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<KnowledgeArticleDetailDto> PublishAsync(Guid articleId, CancellationToken cancellationToken = default);

    Task<KnowledgeArticleDetailDto> ArchiveAsync(Guid articleId, CancellationToken cancellationToken = default);
}
