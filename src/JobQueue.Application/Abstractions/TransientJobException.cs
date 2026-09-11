namespace JobQueue.Application.Abstractions;

/// <summary>
/// A job failure that is expected to be temporary and may succeed on a retry, e.g.
/// a database outage, a timeout, or a message-broker problem. Handlers can wrap
/// lower-level exceptions in this type to make the retry intent explicit.
/// </summary>
public sealed class TransientJobException : Exception
{
    public TransientJobException(string message)
        : base(message)
    {
    }

    public TransientJobException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
