namespace JobQueue.Domain.Exceptions;

/// <summary>
/// Thrown when a job lifecycle operation is requested from a state that does not
/// allow it (e.g. cancelling a completed job, retrying a pending job).
/// </summary>
public sealed class InvalidJobTransitionException : Exception
{
    public InvalidJobTransitionException(string message)
        : base(message)
    {
    }
}
