namespace JobQueue.Application.Abstractions;

/// <summary>
/// Classifies job execution errors as transient (worth retrying) or permanent
/// (retrying will never succeed).
/// </summary>
public interface IJobErrorClassifier
{
    /// <summary>
    /// Returns true when the failure is expected to be temporary and the job should
    /// be retried; false when the job should fail permanently.
    /// </summary>
    bool IsTransient(Exception exception);
}
