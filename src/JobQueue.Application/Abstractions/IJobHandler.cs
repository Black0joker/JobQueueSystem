namespace JobQueue.Application.Abstractions;

/// <summary>
/// Executes one logical job type. Implementations are resolved through
/// <see cref="IJobHandlerResolver"/> based on <see cref="JobType"/>, which keeps the
/// queue infrastructure independent from the actual business operations.
/// </summary>
public interface IJobHandler
{
    /// <summary>The logical job type this handler processes (e.g. "SendEmail").</summary>
    string JobType { get; }

    /// <summary>
    /// Executes the job. Throwing an exception marks the current attempt as failed.
    /// </summary>
    Task HandleAsync(JobExecutionContext context, CancellationToken cancellationToken = default);
}
