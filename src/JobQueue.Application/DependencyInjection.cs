using System.Reflection;
using JobQueue.Application.Abstractions;
using JobQueue.Application.Jobs.Commands;
using JobQueue.Application.Jobs.Queries;
using JobQueue.Application.Jobs.Services;
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

    /// <summary>
    /// Discovers and registers every concrete <see cref="IJobHandler"/> in the given
    /// assemblies, plus the <see cref="IJobHandlerResolver"/> used to look them up by
    /// job type. The worker passes its own assembly; the API needs no handlers.
    /// </summary>
    public static IServiceCollection AddJobHandlers(this IServiceCollection services, params Assembly[] assemblies)
    {
        var handlerTypes = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IJobHandler).IsAssignableFrom(type));

        foreach (var handlerType in handlerTypes)
        {
            services.AddScoped(typeof(IJobHandler), handlerType);
        }

        services.AddScoped<IJobHandlerResolver, JobHandlerResolver>();

        return services;
    }
}
