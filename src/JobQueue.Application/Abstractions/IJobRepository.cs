using JobQueue.Domain.Jobs;

namespace JobQueue.Application.Abstractions;

/// <summary>
/// Persistence port for <see cref="Job"/> data. Implemented by the infrastructure layer.
/// </summary>
public interface IJobRepository
{
    /// <summary>Loads a job by its identifier, or null when it does not exist.</summary>
    Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads a job by its idempotency key, or null when it does not exist.</summary>
    Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Adds a new job to the persistence store.</summary>
    Task AddAsync(Job job, CancellationToken cancellationToken = default);

    /// <summary>Commits pending changes to the persistence store.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
