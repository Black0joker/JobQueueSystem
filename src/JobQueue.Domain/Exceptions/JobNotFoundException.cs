namespace JobQueue.Domain.Exceptions;

/// <summary>
/// Thrown when an operation references a job that does not exist.
/// </summary>
public sealed class JobNotFoundException : Exception
{
    /// <summary>The identifier that was not found.</summary>
    public Guid JobId { get; }

    public JobNotFoundException(Guid jobId)
        : base($"No job exists with id '{jobId}'.")
    {
        JobId = jobId;
    }
}
