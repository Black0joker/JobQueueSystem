namespace JobQueue.Application.Abstractions;

/// <summary>
/// A job failure that will never succeed no matter how often it is retried, e.g.
/// an invalid payload, an unknown referenced resource, or violated business rules.
/// Jobs failing with this exception go straight to Failed without retrying.
/// </summary>
public sealed class PermanentJobException : Exception
{
    public PermanentJobException(string message)
        : base(message)
    {
    }

    public PermanentJobException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
