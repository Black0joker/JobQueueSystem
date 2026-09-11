namespace JobQueue.Domain.Jobs;

/// <summary>
/// A single execution attempt of a <see cref="Job"/>, providing a full execution history
/// (one row per attempt instead of only the last error).
/// </summary>
public class JobAttempt
{
    /// <summary>Unique identifier of the attempt.</summary>
    public Guid Id { get; set; }

    /// <summary>Identifier of the job this attempt belongs to.</summary>
    public Guid JobId { get; set; }

    /// <summary>One-based attempt number.</summary>
    public int AttemptNumber { get; set; }

    /// <summary>UTC timestamp when the attempt started.</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>UTC timestamp when the attempt completed successfully, if it did.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>UTC timestamp when the attempt failed, if it did.</summary>
    public DateTime? FailedAt { get; set; }

    /// <summary>The error message when the attempt failed.</summary>
    public string? Error { get; set; }

    /// <summary>Identifier of the worker that executed the attempt.</summary>
    public string? WorkerId { get; set; }

    /// <summary>The job this attempt belongs to.</summary>
    public Job Job { get; set; } = default!;
}
