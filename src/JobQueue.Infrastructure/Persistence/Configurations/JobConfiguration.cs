using JobQueue.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobQueue.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Job"/> to the Jobs table.
/// </summary>
public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();

        builder.Property(j => j.Type).HasMaxLength(200).IsRequired();
        builder.Property(j => j.Payload).IsRequired(); // nvarchar(max)
        builder.Property(j => j.Status).HasConversion<int>();
        builder.Property(j => j.Priority).IsRequired();
        builder.Property(j => j.Attempts).IsRequired();
        builder.Property(j => j.MaxAttempts).IsRequired();
        builder.Property(j => j.CreatedAt).IsRequired();
        builder.Property(j => j.ScheduledAt);
        builder.Property(j => j.StartedAt);
        builder.Property(j => j.CompletedAt);
        builder.Property(j => j.FailedAt);
        builder.Property(j => j.CancelledAt);
        builder.Property(j => j.LastError); // nvarchar(max)
        builder.Property(j => j.IdempotencyKey).HasMaxLength(200);
        builder.Property(j => j.CorrelationId);
        builder.Property(j => j.WorkerId).HasMaxLength(200);

        // Optimistic concurrency token (SQL Server rowversion).
        builder.Property(j => j.RowVersion).IsRowVersion();

        // Query paths: picking up due scheduled work and filtering by status.
        builder.HasIndex(j => new { j.Status, j.ScheduledAt });

        // Idempotent creation: unique only when a key is provided.
        builder.HasIndex(j => j.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        builder.HasMany(j => j.JobAttempts)
            .WithOne(a => a.Job)
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
