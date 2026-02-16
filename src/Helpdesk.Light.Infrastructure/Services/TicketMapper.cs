using Helpdesk.Light.Application.Contracts.Tickets;
using Helpdesk.Light.Domain.Ai;
using Helpdesk.Light.Domain.Tickets;

namespace Helpdesk.Light.Infrastructure.Services;

internal static class TicketMapper
{
    public static TicketSummaryDto ToSummary(Ticket ticket, TicketAiSuggestion? latestSuggestion)
    {
        return new TicketSummaryDto(
            ticket.Id,
            ticket.ReferenceCode,
            ticket.CustomerId,
            ticket.CreatedByUserId,
            ticket.Channel,
            ticket.Status,
            ticket.Priority,
            ticket.Category,
            ticket.Subject,
            ticket.Summary,
            ticket.AssignedToUserId,
            ticket.ResolverGroupId,
            ticket.CreatedUtc,
            ticket.UpdatedUtc,
            ticket.ResolvedUtc,
            latestSuggestion?.Status.ToString(),
            latestSuggestion?.Confidence);
    }

    public static TicketMessageDto ToDto(TicketMessage message)
    {
        return new TicketMessageDto(
            message.Id,
            message.TicketId,
            message.AuthorType,
            message.AuthorUserId,
            message.Body,
            message.Source,
            message.ExternalMessageId,
            message.CreatedUtc);
    }

    public static TicketAttachmentDto ToDto(TicketAttachment attachment)
    {
        return new TicketAttachmentDto(
            attachment.Id,
            attachment.TicketId,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.CreatedUtc);
    }

    public static TicketAiSuggestionDto ToDto(TicketAiSuggestion suggestion)
    {
        return new TicketAiSuggestionDto(
            suggestion.Id,
            suggestion.TicketId,
            suggestion.DraftResponse,
            suggestion.SuggestedCategory,
            suggestion.SuggestedPriority,
            suggestion.RiskLevel,
            suggestion.Confidence,
            suggestion.Status,
            suggestion.CreatedUtc,
            suggestion.UpdatedUtc,
            suggestion.ProcessedByUserId);
    }
}
