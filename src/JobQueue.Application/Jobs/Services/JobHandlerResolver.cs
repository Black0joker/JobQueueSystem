using JobQueue.Application.Abstractions;

namespace JobQueue.Application.Jobs.Services;

/// <summary>
/// Maps job types to their registered <see cref="IJobHandler"/> implementations.
/// Lookups are case-insensitive; registering two handlers for the same job type is a
/// configuration error detected at startup.
/// </summary>
public sealed class JobHandlerResolver : IJobHandlerResolver
{
    private readonly Dictionary<string, IJobHandler> _handlersByType;

    public JobHandlerResolver(IEnumerable<IJobHandler> handlers)
    {
        _handlersByType = new Dictionary<string, IJobHandler>(StringComparer.OrdinalIgnoreCase);

        foreach (var handler in handlers)
        {
            if (!_handlersByType.TryAdd(handler.JobType, handler))
            {
                throw new InvalidOperationException(
                    $"Multiple job handlers are registered for job type '{handler.JobType}'.");
            }
        }
    }

    public IJobHandler? Resolve(string jobType)
    {
        if (string.IsNullOrWhiteSpace(jobType))
        {
            return null;
        }

        return _handlersByType.GetValueOrDefault(jobType);
    }
}
