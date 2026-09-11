using JobQueue.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobQueue.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="JobAttempt"/> to the JobAttempts table.
/// </summary>
public class JobAttemptConfiguration : IEntityTypeConfiguration<JobAttempt>
{
    public void Configure(EntityTypeBuilder<JobAttempt> builder)
    {
        builder.ToTable("JobAttempts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.JobId).IsRequired();
        builder.Property(a => a.AttemptNumber).IsRequired();
        builder.Property(a => a.StartedAt).IsRequired();
        builder.Property(a => a.CompletedAt);
        builder.Property(a => a.FailedAt);
        builder.Property(a => a.Error); // nvarchar(max)
        builder.Property(a => a.WorkerId).HasMaxLength(200);

        builder.HasIndex(a => a.JobId);
    }
}
