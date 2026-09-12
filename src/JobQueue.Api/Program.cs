using JobQueue.Api.Middleware;
using JobQueue.Application;
using JobQueue.Infrastructure;
using JobQueue.Infrastructure.Health;
using JobQueue.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Phase 24 health checks: /health/ready fails while SQL Server or RabbitMQ is
// unavailable; /health is a dependency-free liveness probe.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<JobQueueDbContext>("sqlserver", tags: new[] { "ready" })
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "ready" });

var app = builder.Build();

// Phase 26: docker/dev convenience - apply pending EF migrations on startup when
// explicitly enabled (docker-compose sets Database__ApplyMigrationsOnStartup=true).
if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<JobQueueDbContext>();
    dbContext.Database.Migrate();
}

// Phase 23: give every request a correlation id (X-Correlation-Id), echo it in the
// response, and enrich all request-scoped log entries with it.
app.UseMiddleware<CorrelationIdMiddleware>();

// Configure the HTTP request pipeline.
// OpenAPI document + Scalar interactive reference UI. Gate these behind
// app.Environment.IsDevelopment() (or authentication) before deploying publicly.
app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("JobQueue API"));

// Phase 15: observability - Prometheus /metrics (including per-request HTTP metrics).
app.UseHttpMetrics();
app.MapMetrics();

// Phase 24: liveness (the process can serve requests) vs readiness (dependencies OK).
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
