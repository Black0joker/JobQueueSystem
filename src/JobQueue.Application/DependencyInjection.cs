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
        // Application services (commands, queries, job services) are registered here
        // in later phases.
        return services;
    }
}
