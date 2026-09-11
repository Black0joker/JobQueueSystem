using JobQueue.Application.Abstractions;
using JobQueue.Infrastructure.Messaging;
using JobQueue.Infrastructure.Persistence;
using JobQueue.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobQueue.Infrastructure;

/// <summary>
/// Composition entry point for the infrastructure layer services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers infrastructure services (persistence, messaging, scheduling).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("JobQueue")
            ?? throw new InvalidOperationException("Connection string 'JobQueue' is not configured.");

        services.AddDbContext<JobQueueDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IJobRepository, JobRepository>();
        services.AddSingleton<IJobPublisher, LoggingJobPublisher>();

        return services;
    }
}
