using MassTransit;
using Microsoft.Extensions.Options;

namespace JobQueue.Worker.Consumers;

/// <summary>
/// Endpoint configuration for <see cref="ProcessJobConsumer"/>: the worker's
/// concurrency limit (phase 10) and the retry policy (phase 12).
/// </summary>
public sealed class ProcessJobConsumerDefinition : ConsumerDefinition<ProcessJobConsumer>
{
    /// <summary>Number of retries after the first attempt (default MaxAttempts = 3).</summary>
    private const int RetryCount = 2;

    /// <summary>Delay between retry attempts.</summary>
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);

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
        // Phase 12: only transient failures are rethrown by the consumer, so only
        // those reach this policy. Together with the per-job MaxAttempts check inside
        // the consumer this yields at most 3 attempts per job. Cancellations caused
        // by host shutdown are never retried in-process; RabbitMQ redelivers them.
        endpointConfigurator.UseMessageRetry(retry => retry
            .Interval(RetryCount, RetryInterval)
            .Ignore<OperationCanceledException>());

        endpointConfigurator.ConcurrentMessageLimit = Math.Max(1, _options.Concurrency);
    }
}
