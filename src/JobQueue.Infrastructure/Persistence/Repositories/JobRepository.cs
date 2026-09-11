using JobQueue.Application.Abstractions;
using JobQueue.Domain.Enums;
using JobQueue.Domain.Exceptions;
using JobQueue.Domain.Jobs;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace JobQueue.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IJobRepository"/>.
/// </summary>
public sealed class JobRepository : IJobRepository
{
    private readonly JobQueueDbContext _dbContext;

    public JobRepository(JobQueueDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public Task<Job?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => _dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<IReadOnlyList<Job>> GetStuckProcessingJobsAsync(
        DateTime heartbeatCutoff,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Jobs
            .Where(j => j.Status == JobStatus.Processing
                && (j.LastHeartbeatAt == null || j.LastHeartbeatAt < heartbeatCutoff))
            .OrderBy(j => j.LastHeartbeatAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Job>> GetDueScheduledJobsAsync(
        DateTime dueBeforeUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Jobs
            .Where(j => j.Status == JobStatus.Scheduled
                && j.ScheduledAt != null
                && j.ScheduledAt <= dueBeforeUtc)
            .OrderBy(j => j.ScheduledAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobAttempt>> GetAttemptsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.JobAttempts.AsNoTracking()
            .Where(a => a.JobId == jobId)
            .OrderBy(a => a.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Job> Items, int TotalCount)> ListAsync(
        JobStatus? status,
        string? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Jobs.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(j => j.Type == type);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Job job, CancellationToken cancellationToken = default)
        => await _dbContext.Jobs.AddAsync(job, cancellationToken);

    public Task RefreshAsync(Job job, CancellationToken cancellationToken = default)
        => _dbContext.Entry(job).ReloadAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new DuplicateJobException("A job with the same idempotency key already exists.", ex);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        // SQL Server error 2601: unique index violation; 2627: unique constraint violation.
        return exception.InnerException is SqlException sqlException
            && sqlException.Errors.Cast<SqlError>().Any(e => e.Number is 2601 or 2627);
    }
}
