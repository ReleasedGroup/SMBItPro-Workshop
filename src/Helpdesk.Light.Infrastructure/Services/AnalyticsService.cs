using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;
using Helpdesk.Light.Application.Errors;
using Helpdesk.Light.Domain.Ai;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class AnalyticsService(
    HelpdeskDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IAnalyticsService
{
    public async Task<AnalyticsDashboardDto> GetDashboardAsync(AnalyticsDashboardRequest request, CancellationToken cancellationToken = default)
    {
        TenantAccessContext context = tenantContextAccessor.Current;
        if (!context.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        DateTime utcNow = DateTime.UtcNow;
        DateTime rangeEndUtc = request.ToUtc?.ToUniversalTime() ?? utcNow;
        DateTime rangeStartUtc = request.FromUtc?.ToUniversalTime() ?? rangeEndUtc.AddDays(-30);
        if (rangeStartUtc > rangeEndUtc)
        {
            throw new InvalidOperationException("Range start must be before range end.");
        }

        Guid? scopedCustomerId = ResolveCustomerScope(context, request.CustomerId);

        IQueryable<Ticket> allTickets = dbContext.Tickets.AsNoTracking();
        if (scopedCustomerId.HasValue)
        {
            allTickets = allTickets.Where(item => item.CustomerId == scopedCustomerId.Value);
        }

        IQueryable<Ticket> rangedTickets = allTickets
            .Where(item => item.CreatedUtc >= rangeStartUtc && item.CreatedUtc <= rangeEndUtc);

        int totalTicketVolume = await rangedTickets.CountAsync(cancellationToken);
        int openTicketCount = await rangedTickets.CountAsync(
            item => item.Status != TicketStatus.Resolved && item.Status != TicketStatus.Closed,
            cancellationToken);

        List<Ticket> openTickets = await rangedTickets
            .Where(item => item.Status != TicketStatus.Resolved && item.Status != TicketStatus.Closed)
            .ToListAsync(cancellationToken);

        Dictionary<string, int> openByPriority = Enum.GetValues<TicketPriority>()
            .ToDictionary(value => value.ToString(), value => 0);
        foreach (Ticket ticket in openTickets)
        {
            string key = ticket.Priority.ToString();
            openByPriority[key] = openByPriority.GetValueOrDefault(key) + 1;
        }

        Dictionary<string, int> channelSplit = Enum.GetValues<TicketChannel>()
            .ToDictionary(value => value.ToString(), value => 0);
        List<Ticket> rangedTicketList = await rangedTickets.ToListAsync(cancellationToken);
        foreach (Ticket ticket in rangedTicketList)
        {
            string key = ticket.Channel.ToString();
            channelSplit[key] = channelSplit.GetValueOrDefault(key) + 1;
        }

        List<FirstResponseProjection> firstResponses = await rangedTickets
            .Select(ticket => new FirstResponseProjection(
                ticket.CreatedUtc,
                dbContext.TicketMessages
                    .Where(message => message.TicketId == ticket.Id &&
                                      (message.AuthorType == TicketAuthorType.Technician ||
                                       message.AuthorType == TicketAuthorType.Agent))
                    .OrderBy(message => message.CreatedUtc)
                    .Select(message => (DateTime?)message.CreatedUtc)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        List<double> firstResponseMinutes = firstResponses
            .Where(item => item.FirstResponseUtc.HasValue)
            .Select(item => (item.FirstResponseUtc!.Value - item.CreatedUtc).TotalMinutes)
            .Where(item => item >= 0)
            .ToList();

        double? averageFirstResponse = firstResponseMinutes.Count == 0
            ? null
            : firstResponseMinutes.Average();

        List<(DateTime CreatedUtc, DateTime ResolvedUtc)> resolvedTickets = await allTickets
            .Where(item => item.ResolvedUtc.HasValue &&
                           item.ResolvedUtc >= rangeStartUtc &&
                           item.ResolvedUtc <= rangeEndUtc)
            .Select(item => new ValueTuple<DateTime, DateTime>(item.CreatedUtc, item.ResolvedUtc!.Value))
            .ToListAsync(cancellationToken);

        List<double> resolutionMinutes = resolvedTickets
            .Select(item => (item.ResolvedUtc - item.CreatedUtc).TotalMinutes)
            .Where(item => item >= 0)
            .ToList();

        double? averageResolution = resolutionMinutes.Count == 0
            ? null
            : resolutionMinutes.Average();

        IQueryable<TicketAiSuggestion> aiSuggestions = dbContext.TicketAiSuggestions.AsNoTracking()
            .Where(item => item.CreatedUtc >= rangeStartUtc && item.CreatedUtc <= rangeEndUtc);

        if (scopedCustomerId.HasValue)
        {
            Guid customerId = scopedCustomerId.Value;
            aiSuggestions = aiSuggestions.Where(item =>
                dbContext.Tickets.Any(ticket => ticket.Id == item.TicketId && ticket.CustomerId == customerId));
        }

        int totalAiSuggestions = await aiSuggestions.CountAsync(cancellationToken);
        int acceptedAiSuggestions = await aiSuggestions.CountAsync(
            item => item.Status == AiSuggestionStatus.Approved || item.Status == AiSuggestionStatus.AutoSent,
            cancellationToken);
        int autoResponseCount = await aiSuggestions.CountAsync(
            item => item.Status == AiSuggestionStatus.AutoSent,
            cancellationToken);

        IQueryable<KnowledgeArticle> aiArticles = dbContext.KnowledgeArticles.AsNoTracking()
            .Where(item => item.AiGenerated && item.CreatedUtc >= rangeStartUtc && item.CreatedUtc <= rangeEndUtc);

        if (scopedCustomerId.HasValue)
        {
            Guid customerId = scopedCustomerId.Value;
            aiArticles = aiArticles.Where(item => item.CustomerId == customerId);
        }

        int aiGeneratedArticleCount = await aiArticles.CountAsync(cancellationToken);
        int publishedAiGeneratedArticleCount = await aiArticles.CountAsync(
            item => item.PublishedUtc.HasValue,
            cancellationToken);

        return new AnalyticsDashboardDto(
            rangeStartUtc,
            rangeEndUtc,
            totalTicketVolume,
            openTicketCount,
            averageFirstResponse,
            averageResolution,
            openByPriority,
            channelSplit,
            totalAiSuggestions,
            acceptedAiSuggestions,
            autoResponseCount,
            aiGeneratedArticleCount,
            publishedAiGeneratedArticleCount,
            RateOrZero(acceptedAiSuggestions, totalAiSuggestions),
            RateOrZero(autoResponseCount, totalAiSuggestions),
            RateOrZero(publishedAiGeneratedArticleCount, aiGeneratedArticleCount));
    }

    private static Guid? ResolveCustomerScope(TenantAccessContext context, Guid? requestedCustomerId)
    {
        if (context.IsMspAdmin)
        {
            return requestedCustomerId;
        }

        if (!context.CustomerId.HasValue)
        {
            throw new UnauthorizedAccessException("Authenticated user is missing tenant context.");
        }

        if (requestedCustomerId.HasValue && requestedCustomerId.Value != context.CustomerId.Value)
        {
            throw new TenantAccessDeniedException("Cannot access analytics for another tenant.");
        }

        return context.CustomerId.Value;
    }

    private static double RateOrZero(int numerator, int denominator)
    {
        if (denominator <= 0)
        {
            return 0;
        }

        return (double)numerator / denominator;
    }

    private sealed record FirstResponseProjection(DateTime CreatedUtc, DateTime? FirstResponseUtc);
}
