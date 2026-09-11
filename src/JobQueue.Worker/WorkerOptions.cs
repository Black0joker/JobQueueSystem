namespace JobQueue.Worker;

/// <summary>
/// Worker-specific runtime settings bound from the "Worker" configuration section.
/// </summary>
public sealed class WorkerOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Worker";

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

    /// <summary>Heartbeat refresh interval as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(HeartbeatIntervalSeconds);

    /// <summary>Heartbeat staleness window as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan HeartbeatTimeout => TimeSpan.FromSeconds(HeartbeatTimeoutSeconds);

    /// <summary>Recovery sweep interval as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan RecoveryInterval => TimeSpan.FromSeconds(RecoveryIntervalSeconds);
}
