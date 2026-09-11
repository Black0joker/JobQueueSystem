using JobQueue.Worker;
using Microsoft.Extensions.Options;
using Prometheus;

namespace JobQueue.Worker.BackgroundServices;

/// <summary>
/// Phase 15: hosts a Kestrel server that exposes the worker's Prometheus metrics at
/// /metrics on <c>Worker:MetricsPort</c>. The worker is a console host without an HTTP
/// pipeline, so a dedicated metrics server is used instead of controller routes.
/// </summary>
public sealed class MetricsEndpointService : BackgroundService
{
    private readonly WorkerOptions _options;
    private readonly ILogger<MetricsEndpointService> _logger;
    private KestrelMetricServer? _server;

    public MetricsEndpointService(IOptions<WorkerOptions> options, ILogger<MetricsEndpointService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _server = new KestrelMetricServer(_options.MetricsPort);
        _server.Start();

        _logger.LogInformation("Prometheus metrics endpoint listening on port {MetricsPort}.", _options.MetricsPort);

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_server is not null)
        {
            _server.Stop();
            _server.Dispose();
            _server = null;
        }

        await base.StopAsync(cancellationToken);
    }
}
