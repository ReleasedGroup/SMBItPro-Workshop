using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Contracts;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class RuntimeMetricsRecorder : IRuntimeMetricsRecorder
{
    private const int MaxFailureEntries = 50;

    private static readonly Meter Meter = new("Helpdesk.Light.Observability", "1.0.0");
    private static readonly Counter<long> ApiRequestCounter = Meter.CreateCounter<long>("helpdesk.api.requests.total");
    private static readonly Counter<long> ApiErrorCounter = Meter.CreateCounter<long>("helpdesk.api.errors.total");
    private static readonly Histogram<double> ApiLatencyHistogram = Meter.CreateHistogram<double>("helpdesk.api.latency.ms", "ms");
    private static readonly Counter<long> EmailOutcomeCounter = Meter.CreateCounter<long>("helpdesk.email.outcomes.total");
    private static readonly Counter<long> AiRunCounter = Meter.CreateCounter<long>("helpdesk.ai.runs.total");
    private static readonly Histogram<double> AiDurationHistogram = Meter.CreateHistogram<double>("helpdesk.ai.duration.ms", "ms");

    private readonly ConcurrentQueue<WorkerFailureDto> recentFailures = new();

    private long apiRequestCount;
    private long apiErrorCount;
    private long apiLatencySampleCount;
    private long apiLatencyMicrosecondsTotal;

    private int workerQueueDepth;

    private long emailSentCount;
    private long emailFailedCount;
    private long emailDeadLetterCount;

    private long aiRunCount;
    private long aiRunFailureCount;
    private long aiDurationSampleCount;
    private long aiDurationMicrosecondsTotal;

    public RuntimeMetricsRecorder()
    {
        Meter.CreateObservableGauge("helpdesk.worker.queue.depth", () => Volatile.Read(ref workerQueueDepth));
    }

    public void RecordApiRequest(int statusCode, double latencyMs)
    {
        Interlocked.Increment(ref apiRequestCount);
        ApiRequestCounter.Add(1);

        if (statusCode >= 400)
        {
            Interlocked.Increment(ref apiErrorCount);
            ApiErrorCounter.Add(1);
        }

        long microseconds = Math.Max(0, (long)(latencyMs * 1000));
        Interlocked.Increment(ref apiLatencySampleCount);
        Interlocked.Add(ref apiLatencyMicrosecondsTotal, microseconds);
        ApiLatencyHistogram.Record(latencyMs);
    }

    public void SetWorkerQueueDepth(int depth)
    {
        Interlocked.Exchange(ref workerQueueDepth, Math.Max(0, depth));
    }

    public void RecordEmailSent()
    {
        Interlocked.Increment(ref emailSentCount);
        EmailOutcomeCounter.Add(1, new KeyValuePair<string, object?>("outcome", "sent"));
    }

    public void RecordEmailFailed()
    {
        Interlocked.Increment(ref emailFailedCount);
        EmailOutcomeCounter.Add(1, new KeyValuePair<string, object?>("outcome", "failed"));
    }

    public void RecordEmailDeadLetter()
    {
        Interlocked.Increment(ref emailDeadLetterCount);
        EmailOutcomeCounter.Add(1, new KeyValuePair<string, object?>("outcome", "dead_letter"));
    }

    public void RecordAiRun(double durationMs, bool success)
    {
        Interlocked.Increment(ref aiRunCount);
        AiRunCounter.Add(1, new KeyValuePair<string, object?>("outcome", success ? "success" : "failed"));

        if (!success)
        {
            Interlocked.Increment(ref aiRunFailureCount);
        }

        long microseconds = Math.Max(0, (long)(durationMs * 1000));
        Interlocked.Increment(ref aiDurationSampleCount);
        Interlocked.Add(ref aiDurationMicrosecondsTotal, microseconds);
        AiDurationHistogram.Record(durationMs, new KeyValuePair<string, object?>("success", success));
    }

    public void RecordWorkerFailure(string worker, string error)
    {
        WorkerFailureDto failure = new(DateTime.UtcNow, worker, error);
        recentFailures.Enqueue(failure);

        while (recentFailures.Count > MaxFailureEntries)
        {
            _ = recentFailures.TryDequeue(out _);
        }
    }

    public OperationsMetricsDto GetSnapshot()
    {
        long apiSamples = Interlocked.Read(ref apiLatencySampleCount);
        long aiSamples = Interlocked.Read(ref aiDurationSampleCount);

        double averageApiLatencyMs = apiSamples == 0
            ? 0
            : Interlocked.Read(ref apiLatencyMicrosecondsTotal) / 1000d / apiSamples;

        double averageAiDurationMs = aiSamples == 0
            ? 0
            : Interlocked.Read(ref aiDurationMicrosecondsTotal) / 1000d / aiSamples;

        return new OperationsMetricsDto(
            DateTime.UtcNow,
            Interlocked.Read(ref apiRequestCount),
            Interlocked.Read(ref apiErrorCount),
            averageApiLatencyMs,
            Volatile.Read(ref workerQueueDepth),
            Interlocked.Read(ref emailSentCount),
            Interlocked.Read(ref emailFailedCount),
            Interlocked.Read(ref emailDeadLetterCount),
            Interlocked.Read(ref aiRunCount),
            Interlocked.Read(ref aiRunFailureCount),
            averageAiDurationMs,
            recentFailures.ToArray());
    }
}
