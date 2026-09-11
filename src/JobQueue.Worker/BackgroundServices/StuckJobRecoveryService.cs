using JobQueue.Application.Abstractions;
using JobQueue.Domain.Enums;
using JobQueue.Domain.Jobs;
using JobQueue.Worker.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobQueue.Worker.BackgroundServices;

/// <summary>
/// Periodically scans for jobs stuck in <see cref="JobStatus.Processing"/> whose owning
/// worker stopped sending heartbeats (crash, hard kill, network loss) and recovers them
/// (phase 14): jobs with attempts remaining are reset to Pending and re-published;
/// exhausted jobs are dead-lettered. The RowVersion concurrency token prevents two
/// workers from recovering the same job, and from recovering a job its original worker
/// just finished.
/// </summary>
public sealed class StuckJobRecoveryService : BackgroundService
{
    /// <summary>Maximum number of stuck jobs recovered per sweep.</summary>
    private const int BatchLimit = 100;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<WorkerOptions> _options;
    private readonly ILogger<StuckJobRecoveryService> _logger;

    public StuckJobRecoveryService(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<StuckJobRecoveryService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        _logger.LogInformation(
            "Stuck-job recovery started (sweep interval {RecoveryInterval}, heartbeat timeout {HeartbeatTimeout}).",
            options.RecoveryInterval,
            options.HeartbeatTimeout);

        using var timer = new PeriodicTimer(options.RecoveryInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RecoverStuckJobsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Host shutdown.
        }
    }

    private async Task RecoverStuckJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IJobRepository>();
            var publisher = scope.ServiceProvider.GetRequiredService<IJobPublisher>();

            var cutoff = DateTime.UtcNow - _options.Value.HeartbeatTimeout;
            var stuckJobs = await repository.GetStuckProcessingJobsAsync(cutoff, BatchLimit, cancellationToken);
            if (stuckJobs.Count == 0)
            {
                return;
            }

            _logger.LogWarning("Found {StuckCount} stuck job(s) in Processing; recovering.", stuckJobs.Count);

            foreach (var job in stuckJobs)
            {
                await RecoverJobAsync(job, repository, publisher, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stuck-job recovery sweep failed; it will retry on the next interval.");
        }
    }

    private async Task RecoverJobAsync(
        Job job,
        IJobRepository repository,
        IJobPublisher publisher,
        CancellationToken cancellationToken)
    {
        try
        {
            if (job.Attempts >= job.MaxAttempts)
            {
                job.Status = JobStatus.DeadLettered;
                job.DeadLetteredAt = DateTime.UtcNow;
                job.LastError = $"Worker heartbeat lost; attempts exhausted ({job.Attempts}/{job.MaxAttempts}).";
                job.WorkerId = null;
                await repository.SaveChangesAsync(cancellationToken);

                JobMetrics.JobsRecovered.WithLabels("dead_lettered").Inc();

                _logger.LogWarning(
                    "Job {JobId} was dead-lettered by recovery because its worker stopped sending heartbeats.",
                    job.Id);
                return;
            }

            job.Status = JobStatus.Pending;
            job.StartedAt = null;
            job.WorkerId = null;
            job.LastHeartbeatAt = null;
            await repository.SaveChangesAsync(cancellationToken);

            await publisher.PublishAsync(job.Id, job.Type, cancellationToken);

            JobMetrics.JobsRecovered.WithLabels("re_dispatched").Inc();

            _logger.LogWarning(
                "Job {JobId} recovered from a stale worker and re-dispatched ({AttemptsUsed}/{MaxAttempts} attempts used).",
                job.Id,
                job.Attempts,
                job.MaxAttempts);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another worker recovered it first, or the original worker made progress.
            _logger.LogInformation(
                "Job {JobId} was already recovered or updated concurrently; skipping.",
                job.Id);
        }
    }
}
