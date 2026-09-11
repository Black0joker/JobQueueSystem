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
    /// <summary>Enqueues the given job for processing.</summary>
    Task PublishAsync(Guid jobId, string jobType, CancellationToken cancellationToken = default);
}
