namespace JobQueue.Application.Abstractions;

/// <summary>
/// Messaging port used to enqueue a job for asynchronous processing.
/// </summary>
/// <remarks>
/// A later phase backs this port with MassTransit/RabbitMQ; the current infrastructure
/// implementation is a logging placeholder so the API works before the message bus exists.
/// </remarks>
public interface IJobPublisher
{
    /// <summary>
    /// Enqueues the given job for processing. The optional correlation id travels with
    /// the message (phase 23) so workers can trace the job end to end.
    /// </summary>
    Task PublishAsync(
        Guid jobId,
        string jobType,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default);
}
