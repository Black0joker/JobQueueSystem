using Prometheus;

namespace JobQueue.Worker.Observability;

/// <summary>
/// Prometheus metrics recorded by the worker (phase 15). Exposed on the worker's
/// dedicated metrics endpoint (KestrelMetricServer, Worker:MetricsPort).
/// </summary>
public static class JobMetrics
{
    /// <summary>Terminal outcomes of job executions: completed, failed, dead_lettered.</summary>
    public static readonly Counter JobsProcessed = Metrics.CreateCounter(
        "jobqueue_jobs_processed_total",
        "Number of job executions that reached a terminal outcome.",
        new CounterConfiguration { LabelNames = ["job_type", "result"] });

    /// <summary>Every processing attempt started (first attempts + retries).</summary>
    public static readonly Counter JobAttempts = Metrics.CreateCounter(
        "jobqueue_job_attempts_total",
        "Number of job processing attempts started.",
        new CounterConfiguration { LabelNames = ["job_type"] });

    /// <summary>Duration of successful job executions, in seconds.</summary>
    public static readonly Histogram JobDuration = Metrics.CreateHistogram(
        "jobqueue_job_duration_seconds",
        "Duration of successful job executions.",
        new HistogramConfiguration { LabelNames = ["job_type"] });

    /// <summary>Stuck jobs recovered by the phase 14 heartbeat recovery service.</summary>
    public static readonly Counter JobsRecovered = Metrics.CreateCounter(
        "jobqueue_jobs_recovered_total",
        "Number of stuck jobs recovered by the heartbeat recovery service.",
        new CounterConfiguration { LabelNames = ["result"] });
}
