using JobQueue.Api.Health;
using JobQueue.Application;
using JobQueue.Infrastructure;
using JobQueue.Infrastructure.Persistence;
using Prometheus;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Phase 15: health checks against the two infrastructure dependencies.
// /health fails when SQL Server or RabbitMQ is unavailable.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<JobQueueDbContext>("sqlserver")
    .AddCheck<RabbitMqHealthCheck>("rabbitmq");

var app = builder.Build();

// Configure the HTTP request pipeline.
// OpenAPI document + Scalar interactive reference UI. Gate these behind
// app.Environment.IsDevelopment() (or authentication) before deploying publicly.
app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("JobQueue API"));

// Phase 15: observability — Prometheus /metrics (including per-request HTTP
// metrics) and the /health endpoint.
app.UseHttpMetrics();
app.MapHealthChecks("/health");
app.MapMetrics();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
