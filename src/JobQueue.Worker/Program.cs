using JobQueue.Application;
using JobQueue.Infrastructure;
using JobQueue.Infrastructure.Health;
using JobQueue.Infrastructure.Persistence;
using JobQueue.Worker;
using JobQueue.Worker.BackgroundServices;

var builder = Host.CreateApplicationBuilder(args);

// Phase 25: graceful shutdown. On SIGTERM/SIGINT the host stops accepting new work and
// gives in-flight jobs up to Worker:ShutdownTimeoutSeconds to finish before force-stopping.
// MassTransit stops consuming and waits for running consumers within this window; the
// consumer rethrows the shutdown cancellation instead of saving a wrong outcome, so an
// interrupted job is redelivered to a healthy worker.
builder.Services.Configure<HostOptions>(options =>
    options.ShutdownTimeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("Worker:ShutdownTimeoutSeconds", WorkerOptions.DefaultShutdownTimeoutSeconds)));

builder.Services.AddApplication();

// Phase 9: discover the IJobHandler implementations hosted by the worker assembly.
builder.Services.AddJobHandlers(typeof(Program).Assembly);

// Phase 10: worker concurrency settings (Worker:Concurrency in appsettings).
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));

// The worker assembly owns the MassTransit consumers that process jobs from RabbitMQ.
builder.Services.AddInfrastructure(builder.Configuration, typeof(Program).Assembly);

// Phase 14: background sweep that recovers jobs whose worker stopped heartbeating.
builder.Services.AddHostedService<StuckJobRecoveryService>();

// Phase 17: dispatches Scheduled jobs once their ScheduledAt time has passed.
builder.Services.AddHostedService<ScheduledJobDispatcherService>();

// Phase 24: readiness probes against the critical infrastructure dependencies. The
// worker's diagnostics endpoint (/health/ready on Worker:MetricsPort) fails while SQL
// Server or RabbitMQ is unavailable, so orchestrators stop routing work to an instance
// that cannot do its job.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<JobQueueDbContext>("sqlserver", tags: new[] { "ready" })
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "ready" });

// Phase 15 + 24: dedicated diagnostics endpoints (/metrics, /health, /health/ready).
builder.Services.AddHostedService<MetricsEndpointService>();

var host = builder.Build();
host.Run();
