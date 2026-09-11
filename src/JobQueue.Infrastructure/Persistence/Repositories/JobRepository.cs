using JobQueue.Application.Abstractions;
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

    public Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => _dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task AddAsync(Job job, CancellationToken cancellationToken = default)
        => await _dbContext.Jobs.AddAsync(job, cancellationToken);

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
