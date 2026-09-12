using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Prometheus;

namespace JobQueue.Worker.BackgroundServices;

/// <summary>
/// Hosts a dedicated Kestrel server on <c>Worker:MetricsPort</c> exposing the worker's
/// diagnostics: Prometheus metrics at /metrics (phase 15) and the phase 24 health
/// endpoints /health (liveness) and /health/ready (readiness). Worker readiness fails
/// while critical infrastructure (SQL Server, RabbitMQ) is unavailable, so orchestrators
/// stop routing work to instances that cannot do their job. The worker is a console host
/// without an HTTP pipeline, hence this separate diagnostics server.
/// </summary>
public sealed class MetricsEndpointService : BackgroundService
{
    private readonly WorkerOptions _options;
    private readonly HealthCheckService _healthChecks;
    private readonly ILogger<MetricsEndpointService> _logger;
    private WebApplication? _app;

    public MetricsEndpointService(
        IOptions<WorkerOptions> options,
        HealthCheckService healthChecks,
        ILogger<MetricsEndpointService> logger)
    {
        _options = options.Value;
        _healthChecks = healthChecks;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://0.0.0.0:{_options.MetricsPort}");

        _app = builder.Build();

        // Phase 15: Prometheus metrics for this worker instance.
        _app.MapMetrics();

        // Phase 24 liveness: the process is up and able to serve requests.
        _app.MapGet("/health", () => Results.Json(new { status = HealthStatus.Healthy.ToString() }));

        // Phase 24 readiness: fails while critical infrastructure is unavailable.
        _app.MapGet("/health/ready", async () =>
        {
            var report = await _healthChecks.CheckHealthAsync(check => check.Tags.Contains("ready"));

            var checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description
            });

            var payload = new { status = report.Status.ToString(), checks };

            return report.Status == HealthStatus.Healthy
                ? Results.Json(payload)
                : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        await _app.StartAsync(stoppingToken);

        _logger.LogInformation(
            "Diagnostics endpoints (/metrics, /health, /health/ready) listening on port {MetricsPort}.",
            _options.MetricsPort);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is not null)
        {
            await _app.StopAsync(cancellationToken);
            await _app.DisposeAsync();
            _app = null;
        }

        await base.StopAsync(cancellationToken);
    }
}
