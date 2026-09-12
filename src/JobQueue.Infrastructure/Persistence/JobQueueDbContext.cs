using JobQueue.Domain.Jobs;
using MassTransit;
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

        // Phase 22: transactional outbox entities (InboxState, OutboxMessage, OutboxState)
        // mapped by MassTransit's EF Core integration.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        

        base.OnModelCreating(modelBuilder);
    }
}
