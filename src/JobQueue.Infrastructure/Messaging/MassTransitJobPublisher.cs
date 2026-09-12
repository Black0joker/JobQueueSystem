using JobQueue.Application.Abstractions;
using JobQueue.Infrastructure.Messaging.Contracts;
using MassTransit;

namespace JobQueue.Infrastructure.Messaging;

/// <summary>
/// MassTransit/RabbitMQ-backed publisher that enqueues jobs for asynchronous processing.
/// </summary>
public sealed class MassTransitJobPublisher : IJobPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitJobPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync(
        Guid jobId,
        string jobType,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default)
        => _publishEndpoint.Publish(
            new ProcessJob { JobId = jobId, Type = jobType },
            // Phase 23: the correlation id travels in the message envelope so consumers
            // can trace the job end to end.
            context => context.CorrelationId = correlationId,
            cancellationToken);
}
