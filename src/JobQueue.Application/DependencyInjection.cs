using JobQueue.Application.Jobs.Commands;
using JobQueue.Application.Jobs.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace JobQueue.Application;

/// <summary>
/// Composition entry point for the application layer services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers application-layer services (commands, queries, job services).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateJobCommandHandler>();
        services.AddScoped<GetJobByIdQueryHandler>();
        services.AddScoped<ListJobsQueryHandler>();
        return services;
    }
}
