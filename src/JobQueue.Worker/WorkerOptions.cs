namespace JobQueue.Worker;

/// <summary>
/// Worker-specific runtime settings bound from the "Worker" configuration section.
/// </summary>
public sealed class WorkerOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Worker";

    /// <summary>Default graceful-shutdown window in seconds (phase 25).</summary>
    public const int DefaultShutdownTimeoutSeconds = 60;

    /// <summary>
    /// Number of messages this worker instance processes concurrently. Defaults to 1;
    /// raise it (and/or run more worker instances) to scale out.
    /// </summary>
    public int Concurrency { get; set; } = 1;

    /// <summary>
    /// Seconds between heartbeat refreshes while this worker processes a job (phase 14).
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Seconds after which a Processing job without a fresh heartbeat is considered
    /// stuck and eligible for recovery by another worker (phase 14).
    /// </summary>
    public int HeartbeatTimeoutSeconds { get; set; } = 30;

    /// <summary>Seconds between stuck-job recovery sweeps (phase 14).</summary>
    public int RecoveryIntervalSeconds { get; set; } = 10;

    /// <summary>Seconds between scheduled-job dispatch sweeps (phase 17).</summary>
    public int DispatchIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Seconds the host waits for in-flight jobs to finish during graceful shutdown
    /// (phase 25). MassTransit stops accepting new messages and drains running consumers
    /// within this window; whatever is still running afterwards is redelivered to a
    /// healthy worker.
    /// </summary>
    public int ShutdownTimeoutSeconds { get; set; } = DefaultShutdownTimeoutSeconds;

    /// <summary>TCP port of the worker's Prometheus metrics endpoint (phase 15).</summary>
    public int MetricsPort { get; set; } = 5209;

    /// <summary>Heartbeat refresh interval as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(HeartbeatIntervalSeconds);

    /// <summary>Heartbeat staleness window as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan HeartbeatTimeout => TimeSpan.FromSeconds(HeartbeatTimeoutSeconds);

    /// <summary>Recovery sweep interval as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan RecoveryInterval => TimeSpan.FromSeconds(RecoveryIntervalSeconds);

    /// <summary>Dispatch sweep interval as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan DispatchInterval => TimeSpan.FromSeconds(DispatchIntervalSeconds);
}
