using JobQueue.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace JobQueue.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the job queue database (SQL Server).
/// </summary>
public class JobQueueDbContext : DbContext
{
    public JobQueueDbContext(DbContextOptions<JobQueueDbContext> options)
        : base(options)
    {
    }

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<JobAttempt> JobAttempts => Set<JobAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobQueueDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
