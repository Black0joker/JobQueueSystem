using MassTransit;
using Microsoft.Extensions.Options;

namespace JobQueue.Worker.Consumers;

/// <summary>
/// Endpoint configuration for <see cref="ProcessJobConsumer"/>. Applies the configured
/// concurrency limit so each worker instance processes up to
/// <see cref="WorkerOptions.Concurrency"/> jobs at the same time; RabbitMQ distributes
/// messages across every worker instance attached to the queue.
/// </summary>
public sealed class ProcessJobConsumerDefinition : ConsumerDefinition<ProcessJobConsumer>
{
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
        endpointConfigurator.ConcurrentMessageLimit = Math.Max(1, _options.Concurrency);
    }
}
