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
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // EF Core (SQL Server), MassTransit (RabbitMQ), and background scheduling
        // registrations are added here in later phases.
        return services;
    }
}
