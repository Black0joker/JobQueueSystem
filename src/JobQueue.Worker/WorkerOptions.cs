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
}
