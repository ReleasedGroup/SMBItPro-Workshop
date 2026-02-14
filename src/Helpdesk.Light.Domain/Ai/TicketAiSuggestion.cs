using Helpdesk.Light.Domain.Tickets;

namespace Helpdesk.Light.Domain.Ai;

public sealed class TicketAiSuggestion
{
    private TicketAiSuggestion()
    {
        DraftResponse = string.Empty;
        SuggestedCategory = string.Empty;
        SuggestedPriority = string.Empty;
        RiskLevel = string.Empty;
    }

    public TicketAiSuggestion(
        Guid id,
        Guid ticketId,
        string draftResponse,
        string suggestedCategory,
        string suggestedPriority,
        string riskLevel,
        double confidence,
        AiSuggestionStatus status,
        DateTime createdUtc)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("Ticket id is required.", nameof(ticketId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(draftResponse);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedCategory);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedPriority);
        ArgumentException.ThrowIfNullOrWhiteSpace(riskLevel);

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        TicketId = ticketId;
        DraftResponse = draftResponse.Trim();
        SuggestedCategory = suggestedCategory.Trim();
        SuggestedPriority = suggestedPriority.Trim();
        RiskLevel = riskLevel.Trim();
        Confidence = confidence;
        Status = status;
        CreatedUtc = createdUtc;
        UpdatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public string DraftResponse { get; private set; }

    public string SuggestedCategory { get; private set; }

    public string SuggestedPriority { get; private set; }

    public string RiskLevel { get; private set; }

    public double Confidence { get; private set; }

    public AiSuggestionStatus Status { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime UpdatedUtc { get; private set; }

    public Guid? ProcessedByUserId { get; private set; }

    public Ticket? Ticket { get; private set; }

    public void SetStatus(AiSuggestionStatus status, Guid? processedByUserId, DateTime utcNow)
    {
        Status = status;
        ProcessedByUserId = processedByUserId;
        UpdatedUtc = utcNow;
    }

    public void UpdateDraftResponse(string response, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(response);
        DraftResponse = response.Trim();
        UpdatedUtc = utcNow;
    }
}
