namespace Helpdesk.Light.Application.Contracts;

public sealed record OperationsMetricsDto(
    DateTime GeneratedUtc,
    long ApiRequestCount,
    long ApiErrorCount,
    double AverageApiLatencyMs,
    int WorkerQueueDepth,
    long EmailSentCount,
    long EmailFailedCount,
    long EmailDeadLetterCount,
    long AiRunCount,
    long AiRunFailureCount,
    double AverageAiDurationMs,
    IReadOnlyList<WorkerFailureDto> RecentWorkerFailures);

public sealed record WorkerFailureDto(DateTime CreatedUtc, string Worker, string Error);

public sealed record DeadLetterMessageDto(
    Guid Id,
    Guid? TicketId,
    Guid CustomerId,
    string ToAddress,
    string Subject,
    int AttemptCount,
    string? LastError,
    DateTime CreatedUtc,
    DateTime? DeadLetteredUtc);
