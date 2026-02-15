using Helpdesk.Light.Application.Contracts;

namespace Helpdesk.Light.Application.Abstractions;

public interface IRuntimeMetricsRecorder
{
    void RecordApiRequest(int statusCode, double latencyMs);

    void SetWorkerQueueDepth(int depth);

    void RecordEmailSent();

    void RecordEmailFailed();

    void RecordEmailDeadLetter();

    void RecordAiRun(double durationMs, bool success);

    void RecordWorkerFailure(string worker, string error);

    OperationsMetricsDto GetSnapshot();
}
