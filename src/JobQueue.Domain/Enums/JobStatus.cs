namespace JobQueue.Domain.Enums;

/// <summary>
/// The lifecycle states of a <see cref="Jobs.Job"/>.
/// </summary>
public enum JobStatus
{
    /// <summary>The job has been created and is waiting to be picked up by a worker.</summary>
    Pending,

    /// <summary>The job is scheduled to run at a future point in time.</summary>
    Scheduled,

    /// <summary>A worker has claimed the job and is currently executing it.</summary>
    Processing,

    /// <summary>The job finished successfully.</summary>
    Completed,

    /// <summary>The job failed but may still be retried.</summary>
    Failed,

    /// <summary>The job was cancelled before it could complete.</summary>
    Cancelled,

    /// <summary>The job exceeded its maximum number of attempts and was moved to the dead-letter queue.</summary>
    DeadLettered
}
