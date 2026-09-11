using JobQueue.Domain.Enums;

namespace JobQueue.Domain.Jobs;

/// <summary>
/// A unit of background work that is created through the API, transported via the
/// message bus, and processed asynchronously by workers.
/// </summary>
public class Job
{
    /// <summary>Default number of processing attempts before a job is dead-lettered.</summary>
    public const int DefaultMaxAttempts = 3;

    /// <summary>Unique identifier of the job.</summary>
    public Guid Id { get; set; }

    /// <summary>The logical job type used to resolve the matching handler (e.g. "SendEmail").</summary>
    public string Type { get; set; } = default!;

    /// <summary>JSON payload describing the job's input data.</summary>
    public string Payload { get; set; } = default!;

    /// <summary>Current lifecycle status of the job.</summary>
    public JobStatus Status { get; set; }

    /// <summary>Priority hint used when dispatching jobs (higher values first).</summary>
    public int Priority { get; set; }

    /// <summary>Number of processing attempts made so far.</summary>
    public int Attempts { get; set; }

    /// <summary>Maximum number of attempts before the job is dead-lettered.</summary>
    public int MaxAttempts { get; set; }

    /// <summary>UTC timestamp when the job was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp when the job should become eligible for processing, if deferred.</summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>UTC timestamp when a worker started processing the job.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>UTC timestamp when the job completed successfully.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>UTC timestamp when the job last failed.</summary>
    public DateTime? FailedAt { get; set; }

    /// <summary>UTC timestamp when the job was cancelled.</summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>UTC timestamp when the job was dead-lettered after exhausting its attempts.</summary>
    public DateTime? DeadLetteredAt { get; set; }

    /// <summary>UTC timestamp of the last heartbeat from the worker owning this job (phase 14).</summary>
    public DateTime? LastHeartbeatAt { get; set; }

    /// <summary>The error message from the most recent failed attempt, if any.</summary>
    public string? LastError { get; set; }

    /// <summary>Caller-supplied key used to guarantee idempotent job creation.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Correlation identifier propagated across the HTTP request, message, and worker processing.</summary>
    public Guid? CorrelationId { get; set; }

    /// <summary>Identifier of the worker currently owning the job, if any.</summary>
    public string? WorkerId { get; set; }

    /// <summary>
    /// Database-managed optimistic concurrency token (SQL Server rowversion).
    /// </summary>
    public byte[] RowVersion { get; set; } = [];

    /// <summary>Execution history of this job's attempts.</summary>
    public ICollection<JobAttempt> JobAttempts { get; set; } = new List<JobAttempt>();

    /// <summary>
    /// Parameterless constructor reserved for EF Core materialization.
    /// </summary>
    private Job()
    {
    }

    /// <summary>
    /// Creates a new job in its initial lifecycle state.
    /// </summary>
    /// <param name="type">The logical job type used to resolve the handler.</param>
    /// <param name="payload">JSON payload describing the job's input data.</param>
    /// <param name="priority">Dispatch priority (higher values first).</param>
    /// <param name="maxAttempts">Maximum attempts before the job is dead-lettered.</param>
    /// <param name="scheduledAt">Optional future UTC execution time; the job starts as <see cref="JobStatus.Scheduled"/>.</param>
    /// <param name="idempotencyKey">Optional key preventing duplicate job creation.</param>
    /// <param name="correlationId">Optional correlation identifier for end-to-end tracing.</param>
    /// <returns>A new job in <see cref="JobStatus.Pending"/> or <see cref="JobStatus.Scheduled"/> state.</returns>
    public static Job Create(
        string type,
        string payload,
        int priority = 0,
        int maxAttempts = DefaultMaxAttempts,
        DateTime? scheduledAt = null,
        string? idempotencyKey = null,
        Guid? correlationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        var now = DateTime.UtcNow;
        var isScheduled = scheduledAt is not null && scheduledAt.Value > now;

        return new Job
        {
            Id = Guid.NewGuid(),
            Type = type,
            Payload = payload,
            Status = isScheduled ? JobStatus.Scheduled : JobStatus.Pending,
            Priority = priority,
            Attempts = 0,
            MaxAttempts = maxAttempts,
            CreatedAt = now,
            ScheduledAt = scheduledAt,
            IdempotencyKey = idempotencyKey,
            CorrelationId = correlationId
        };
    }
}
