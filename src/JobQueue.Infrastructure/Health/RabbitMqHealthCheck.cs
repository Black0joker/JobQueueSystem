using System.Net.Sockets;
using JobQueue.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace JobQueue.Infrastructure.Health;

/// <summary>
/// Phase 15/24 health check: verifies that the RabbitMQ broker's AMQP port is reachable.
/// Shared by the API and worker readiness endpoints.
/// </summary>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);

    private readonly RabbitMqOptions _options;

    public RabbitMqHealthCheck(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ConnectTimeout);

            await client.ConnectAsync(_options.Host, RabbitMqOptions.AmqpPort, timeout.Token);

            return HealthCheckResult.Healthy(
                $"RabbitMQ at {_options.Host}:{RabbitMqOptions.AmqpPort} is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"Cannot reach RabbitMQ at {_options.Host}:{RabbitMqOptions.AmqpPort}.",
                ex);
        }
    }
}
