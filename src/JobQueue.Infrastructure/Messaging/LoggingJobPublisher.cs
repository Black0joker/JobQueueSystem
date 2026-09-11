using JobQueue.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace JobQueue.Infrastructure.Messaging;

/// <summary>
/// Placeholder publisher that logs enqueued jobs.
/// </summary>
/// <remarks>
/// This keeps the create-and-enqueue contract working before the message bus exists.
/// It is replaced by a MassTransit/RabbitMQ-backed publisher in a later phase.
/// </remarks>
public sealed class LoggingJobPublisher : IJobPublisher
{
    private readonly ILogger<LoggingJobPublisher> _logger;

    public LoggingJobPublisher(ILogger<LoggingJobPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(Guid jobId, string jobType, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Enqueued job {JobId} of type {JobType} (placeholder transport; RabbitMQ/MassTransit arrives in a later phase).",
            jobId,
            jobType);

        return Task.CompletedTask;
    }
}
