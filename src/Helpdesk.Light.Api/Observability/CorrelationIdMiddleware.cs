using System.Diagnostics;
using Helpdesk.Light.Application.Abstractions;

namespace Helpdesk.Light.Api.Observability;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, IRuntimeMetricsRecorder runtimeMetrics)
    {
        string correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        Stopwatch stopwatch = Stopwatch.StartNew();
        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId
        });

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            int statusCode = context.Response.StatusCode;
            runtimeMetrics.RecordApiRequest(statusCode, stopwatch.Elapsed.TotalMilliseconds);

            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {DurationMs:N1}ms",
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                statusCode,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            string? candidate = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim();
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}
