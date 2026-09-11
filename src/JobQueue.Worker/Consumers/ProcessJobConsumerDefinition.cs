using MassTransit;
using Microsoft.Extensions.Options;

namespace JobQueue.Worker.Consumers;

/// <summary>
/// Endpoint configuration for <see cref="ProcessJobConsumer"/>: the worker's
/// concurrency limit (phase 10) and the exponential-backoff retry policy (phases 12/13).
/// </summary>
public sealed class ProcessJobConsumerDefinition : ConsumerDefinition<ProcessJobConsumer>
{
    /// <summary>Number of retries after the first attempt (default MaxAttempts = 3).</summary>
    private const int RetryCount = 2;

    /// <summary>Smallest delay between retry attempts.</summary>
    private static readonly TimeSpan MinRetryDelay = TimeSpan.FromSeconds(2);

    /// <summary>Largest delay between retry attempts.</summary>
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(8);

    /// <summary>Base growth of the exponential delay (2s, 4s, capped at 8s).</summary>
    private static readonly TimeSpan RetryDelayDelta = TimeSpan.FromSeconds(2);

    private readonly WorkerOptions _options;

    public ProcessJobConsumerDefinition(IOptions<WorkerOptions> options)
    {
        _options = options.Value;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<ProcessJobConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Phase 13: exponential backoff instead of a flat interval. Only transient
        // failures are rethrown by the consumer, so only those reach this policy;
        // together with the per-job MaxAttempts check this yields at most 3 attempts.
        // Cancellations caused by host shutdown are never retried in-process; RabbitMQ
        // redelivers them. When the retries are exhausted the consumer dead-letters the
        // job and rethrows, and MassTransit moves the message to the _error queue (DLQ).
        endpointConfigurator.UseMessageRetry(retry => retry
            .Exponential(RetryCount, MinRetryDelay, MaxRetryDelay, RetryDelayDelta)
            .Ignore<OperationCanceledException>());

        endpointConfigurator.ConcurrentMessageLimit = Math.Max(1, _options.Concurrency);
    }
}
