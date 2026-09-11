using JobQueue.Application.Abstractions;

namespace JobQueue.Application.Jobs.Services;

/// <summary>
/// Base error classification rules. Explicit marker exceptions always win:
/// <see cref="TransientJobException"/> is transient, <see cref="PermanentJobException"/>
/// is permanent. Well-known framework exceptions are mapped explicitly, and anything
/// unknown defaults to transient (retry first, fail later) which is the common
/// queue-system behaviour.
/// </summary>
public class JobErrorClassifier : IJobErrorClassifier
{
    public virtual bool IsTransient(Exception exception)
        => exception switch
        {
            TransientJobException => true,
            PermanentJobException => false,

            // Invalid input / missing resources can never succeed on retry.
            ArgumentException => false,
            KeyNotFoundException => false,

            // Timeouts, cancellations, I/O and HTTP problems are typically temporary.
            TimeoutException => true,
            OperationCanceledException => true,
            IOException => true,
            HttpRequestException => true,

            // Default: unknown failures are treated as transient and retried.
            _ => true
        };
}
