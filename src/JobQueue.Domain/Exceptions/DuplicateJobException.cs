namespace JobQueue.Domain.Exceptions;

/// <summary>
/// Thrown when persisting a job conflicts with an existing job that shares the same
/// idempotency key (unique-index violation).
/// </summary>
public class DuplicateJobException : Exception
{
    public DuplicateJobException()
    {
    }

    public DuplicateJobException(string message)
        : base(message)
    {
    }

    public DuplicateJobException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
