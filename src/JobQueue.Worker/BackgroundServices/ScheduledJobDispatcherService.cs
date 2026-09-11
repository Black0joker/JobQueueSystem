using JobQueue.Application.Abstractions;
using JobQueue.Domain.Enums;
using JobQueue.Domain.Jobs;
using JobQueue.Worker.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobQueue.Worker.BackgroundServices;

/// <summary>
/// Dispatches scheduled jobs when they become due (phase 17). Every sweep loads jobs
/// in <see cref="JobStatus.Scheduled"/> whose <c>ScheduledAt</c> has passed, flips
/// them to <see cref="JobStatus.Pending"/>, and publishes the ProcessJob message.
/// </summary>
/// <remarks>
/// Multiple worker instances may run this dispatcher concurrently. The Scheduled to
/// Pending transition is guarded by the job's RowVersion optimistic concurrency
/// token: when two instances sweep the same due job, only the first save commits and
/// publishes; the loser gets a <see cref="DbUpdateConcurrencyException"/> and skips,
/// so each job is dispatched exactly once. If publishing fails after the save, the
/// job stays Pending and the later outbox phase closes that dual-write gap.
/// </remarks>
public sealed class ScheduledJobDispatcherService : BackgroundService
{
    /// <summary>Maximum number of due jobs dispatched per sweep.</summary>
    private const int BatchLimit = 100;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<WorkerOptions> _options;
    private readonly ILogger<ScheduledJobDispatcherService> _logger;

    public ScheduledJobDispatcherService(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<ScheduledJobDispatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        _logger.LogInformation(
            "Scheduled-job dispatcher started (sweep interval {DispatchInterval}).",
            options.DispatchInterval);

        using var timer = new PeriodicTimer(options.DispatchInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await DispatchDueJobsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Host shutdown.
        }
    }

    private async Task DispatchDueJobsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IJobRepository>();
            var publisher = scope.ServiceProvider.GetRequiredService<IJobPublisher>();

            var dueJobs = await repository.GetDueScheduledJobsAsync(DateTime.UtcNow, BatchLimit, cancellationToken);
            if (dueJobs.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Found {DueCount} due scheduled job(s); dispatching.", dueJobs.Count);

            foreach (var job in dueJobs)
            {
                await DispatchJobAsync(job, repository, publisher, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled-job dispatch sweep failed; it will retry on the next interval.");
        }
    }

    private async Task DispatchJobAsync(
        Job job,
        IJobRepository repository,
        IJobPublisher publisher,
        CancellationToken cancellationToken)
    {
        try
        {
            // Scheduled -> Pending (phase 20 state machine). The RowVersion token makes
            // this atomic across dispatcher instances: only the first committer publishes
            // the message.
            job.TransitionTo(JobStatus.Pending);
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another instance dispatched it first, or it was cancelled in the meantime.
            _logger.LogInformation(
                "Scheduled job {JobId} was already dispatched or updated concurrently; skipping.",
                job.Id);
            return;
        }

        await publisher.PublishAsync(job.Id, job.Type, cancellationToken);

        JobMetrics.ScheduledDispatched.Inc();

        _logger.LogInformation(
            "Scheduled job {JobId} of type {JobType} became due (scheduled for {ScheduledAt:O}) and was dispatched.",
            job.Id,
            job.Type,
            job.ScheduledAt);
    }
}
