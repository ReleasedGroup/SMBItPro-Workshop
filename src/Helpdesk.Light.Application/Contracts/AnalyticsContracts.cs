namespace Helpdesk.Light.Application.Contracts;

public sealed record AnalyticsDashboardRequest(
    Guid? CustomerId,
    DateTime? FromUtc,
    DateTime? ToUtc);

public sealed record AnalyticsDashboardDto(
    DateTime RangeStartUtc,
    DateTime RangeEndUtc,
    int TotalTicketVolume,
    int OpenTicketCount,
    double? AverageFirstResponseMinutes,
    double? AverageResolutionMinutes,
    IReadOnlyDictionary<string, int> OpenTicketsByPriority,
    IReadOnlyDictionary<string, int> ChannelSplit,
    int TotalAiSuggestions,
    int AcceptedAiSuggestions,
    int AutoResponseCount,
    int AiGeneratedArticleCount,
    int PublishedAiGeneratedArticleCount,
    double SuggestionAcceptanceRate,
    double AutoResponseRate,
    double ArticleDraftAcceptanceRate);
