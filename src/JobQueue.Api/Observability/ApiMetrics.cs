using Prometheus;

namespace JobQueue.Api.Observability;

/// <summary>
/// Prometheus metrics recorded by the API (phase 15). Exposed at /metrics.
/// </summary>
public static class ApiMetrics
{
    /// <summary>Number of jobs created through the API, by job type.</summary>
    public static readonly Counter JobsCreated = Metrics.CreateCounter(
        "jobqueue_jobs_created_total",
        "Number of jobs created through the API.",
        new CounterConfiguration { LabelNames = ["job_type"] });

    /// <summary>Number of manual retry requests accepted by the API (phase 16).</summary>
    public static readonly Counter JobsRetried = Metrics.CreateCounter(
        "jobqueue_jobs_retried_total",
        "Number of manual job retry requests accepted by the API.");

    /// <summary>Number of cancellation requests accepted by the API (phase 16).</summary>
    public static readonly Counter JobsCancelled = Metrics.CreateCounter(
        "jobqueue_jobs_cancelled_total",
        "Number of job cancellation requests accepted by the API.");
}
