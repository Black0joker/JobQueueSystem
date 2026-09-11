using JobQueue.Application;
using JobQueue.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();

// The worker assembly owns the MassTransit consumers that process jobs from RabbitMQ.
builder.Services.AddInfrastructure(builder.Configuration, typeof(Program).Assembly);

var host = builder.Build();
host.Run();
