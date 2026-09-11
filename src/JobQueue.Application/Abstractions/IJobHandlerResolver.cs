namespace JobQueue.Application.Abstractions;

/// <summary>
/// Resolves the <see cref="IJobHandler"/> registered for a job type.
/// </summary>
public interface IJobHandlerResolver
{
    /// <summary>
    /// Returns the handler registered for <paramref name="jobType"/>, or null when no
    /// handler is registered for that type.
    /// </summary>
    IJobHandler? Resolve(string jobType);
}
