using JobQueue.Application;
using JobQueue.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var host = builder.Build();
host.Run();
