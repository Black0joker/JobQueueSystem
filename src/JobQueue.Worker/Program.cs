using JobQueue.Application;
using JobQueue.Infrastructure;
using JobQueue.Worker;
using JobQueue.Worker.BackgroundServices;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();

// Phase 9: discover the IJobHandler implementations hosted by the worker assembly.
builder.Services.AddJobHandlers(typeof(Program).Assembly);

// Phase 10: worker concurrency settings (Worker:Concurrency in appsettings).
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));

// The worker assembly owns the MassTransit consumers that process jobs from RabbitMQ.
builder.Services.AddInfrastructure(builder.Configuration, typeof(Program).Assembly);

// Phase 14: background sweep that recovers jobs whose worker stopped heartbeating.
builder.Services.AddHostedService<StuckJobRecoveryService>();

// Phase 15: dedicated Prometheus metrics endpoint for the worker (/metrics).
builder.Services.AddHostedService<MetricsEndpointService>();

var host = builder.Build();
host.Run();
