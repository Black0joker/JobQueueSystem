using System.Reflection;
using JobQueue.Application.Abstractions;
using JobQueue.Infrastructure.Messaging;

using JobQueue.Infrastructure.Persistence;
using JobQueue.Infrastructure.Persistence.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobQueue.Infrastructure;

/// <summary>
/// Composition entry point for the infrastructure layer services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers infrastructure services (persistence, messaging).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="consumerAssemblies">
    /// Assemblies scanned for MassTransit consumers. The worker passes its own assembly;
    /// the API passes none because it only publishes messages.
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] consumerAssemblies)
    {
        var connectionString = configuration.GetConnectionString("JobQueue")
            ?? throw new InvalidOperationException("Connection string 'JobQueue' is not configured.");

        services.AddDbContext<JobQueueDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IJobRepository, JobRepository>();

        // Replace the application-level classifier with one that also understands
        // database errors (transient).
        services.Replace(ServiceDescriptor.Scoped<IJobErrorClassifier, DbAwareJobErrorClassifier>());

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            foreach (var assembly in consumerAssemblies)
            {
                x.AddConsumers(assembly);
            }

            x.UsingRabbitMq((context, cfg) =>
            {
                var options = configuration.GetSection(RabbitMqOptions.SectionName)
                                  .Get<RabbitMqOptions>() ?? new RabbitMqOptions();

                cfg.Host(options.Host, h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<IJobPublisher, MassTransitJobPublisher>();

        return services;
    }
}
