using JobQueue.Domain.Enums;
using JobQueue.Domain.Jobs;

namespace JobQueue.Application.Abstractions;

/// <summary>
/// Persistence port for <see cref="Job"/> data. Implemented by the infrastructure layer.
/// </summary>
public interface IJobRepository
{
    /// <summary>Loads a job by its identifier, or null when it does not exist.</summary>
    Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a job by its identifier with change tracking enabled, so the caller can
    /// update and save it.
    /// </summary>
    Task<Job?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads a job by its idempotency key, or null when it does not exist.</summary>
    Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads up to <paramref name="limit"/> tracked jobs stuck in Processing whose
    /// owning worker has not refreshed its heartbeat since <paramref name="heartbeatCutoff"/>
    /// (phase 14 stuck-job recovery).
    /// </summary>
    Task<IReadOnlyList<Job>> GetStuckProcessingJobsAsync(
        DateTime heartbeatCutoff,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists jobs with optional status/type filtering and pagination, newest first.
    /// </summary>
    /// <param name="status">Optional status filter.</param>
    /// <param name="type">Optional job type filter.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    Task<(IReadOnlyList<Job> Items, int TotalCount)> ListAsync(
        JobStatus? status,
        string? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a new job to the persistence store.</summary>
    Task AddAsync(Job job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads the tracked job's current values (including the concurrency token) from
    /// the persistence store, discarding unsaved changes. Used to resynchronize after
    /// concurrent out-of-band updates such as heartbeat refreshes.
    /// </summary>
    Task RefreshAsync(Job job, CancellationToken cancellationToken = default);

    /// <summary>Commits pending changes to the persistence store.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
