namespace Helpdesk.Light.Domain.Ai;

public sealed class AiRun
{
    private AiRun()
    {
        Model = string.Empty;
        Mode = string.Empty;
        PromptHash = string.Empty;
        Outcome = string.Empty;
    }

    public AiRun(
        Guid id,
        Guid ticketId,
        string model,
        string mode,
        string promptHash,
        int inputTokens,
        int outputTokens,
        double confidence,
        string outcome,
        DateTime createdUtc)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("Ticket id is required.", nameof(ticketId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        TicketId = ticketId;
        Model = model.Trim();
        Mode = mode.Trim();
        PromptHash = promptHash.Trim();
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        Confidence = confidence;
        Outcome = outcome.Trim();
        CreatedUtc = createdUtc;
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public string Model { get; private set; }

    public string Mode { get; private set; }

    public string PromptHash { get; private set; }

    public int InputTokens { get; private set; }

    public int OutputTokens { get; private set; }

    public double Confidence { get; private set; }

    public string Outcome { get; private set; }

    public DateTime CreatedUtc { get; private set; }
}
