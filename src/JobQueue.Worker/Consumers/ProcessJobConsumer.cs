using System.Text.Json;
using JobQueue.Application.Abstractions;
using JobQueue.Domain.Enums;
using JobQueue.Domain.Jobs;
using JobQueue.Infrastructure.Messaging.Contracts;
using JobQueue.Worker.Services;
using JobQueue.Worker.Observability;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobQueue.Worker.Consumers;

/// <summary>
/// Consumes <see cref="ProcessJob"/> messages and processes the referenced job:
/// load from SQL Server, validate the current status, claim it as Processing through
/// the phase 20 state machine, resolve and execute the matching <see cref="IJobHandler"/>,
/// then mark Completed, Failed, or DeadLettered.
/// </summary>
/// <remarks>
/// RabbitMQ provides at-least-once delivery, so the same message may arrive more than
/// once. Only jobs in <see cref="JobStatus.Pending"/> (or <see cref="JobStatus.Processing"/>
/// owned by this worker, i.e. one of our own retries) are executed; every other status
/// means the work was already done, is owned by another worker, or must not run anymore.
/// Every execution is recorded as a <see cref="JobAttempt"/> row (phase 19), giving a
/// full history instead of only the last error. Failed attempts follow the phase 11-13
/// rules: transient errors are rethrown and retried with exponential backoff until
/// <see cref="Job.MaxAttempts"/> is reached, then the job is dead-lettered and the
/// message moves to the DLQ; permanent errors fail the job immediately without retrying.
/// While executing, the worker refreshes the job's heartbeat so the phase 14 recovery
/// service can detect crashes.
/// </remarks>
public sealed class ProcessJobConsumer : IConsumer<ProcessJob>
{
    private readonly IJobRepository _jobRepository;
    private readonly IJobHandlerResolver _handlerResolver;
    private readonly IJobErrorClassifier _errorClassifier;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<WorkerOptions> _options;
    private readonly ILogger<ProcessJobConsumer> _logger;

    public ProcessJobConsumer(
        IJobRepository jobRepository,
        IJobHandlerResolver handlerResolver,
        IJobErrorClassifier errorClassifier,
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<ProcessJobConsumer> logger)
    {
        _jobRepository = jobRepository;
        _handlerResolver = handlerResolver;
        _errorClassifier = errorClassifier;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProcessJob> context)
    {
        var jobId = context.Message.JobId;
        var cancellationToken = context.CancellationToken;
        var workerId = $"{Environment.MachineName}-{Environment.ProcessId}";

        var job = await _jobRepository.GetTrackedByIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            _logger.LogWarning("Received ProcessJob message for unknown job {JobId}; ignoring it.", jobId);
            return;
        }

        switch (job.Status)
        {
            case JobStatus.Completed:
            case JobStatus.Cancelled:
            case JobStatus.Failed:
            case JobStatus.DeadLettered:
                _logger.LogInformation(
                    "Job {JobId} is already {JobStatus}; skipping duplicate delivery.",
                    jobId,
                    job.Status);
                return;

            case JobStatus.Processing when job.WorkerId != workerId:
                _logger.LogWarning(
                    "Job {JobId} is already being processed by worker {WorkerId}; skipping duplicate delivery.",
                    jobId,
                    job.WorkerId);
                return;

            case JobStatus.Processing:
                // Owned by this worker: a retry attempt for a previously failed execution.
                break;

            case JobStatus.Scheduled:
                // The phase 17 dispatcher flips jobs to Pending before publishing, so
                // a Scheduled message here is a race/leftover; the dispatcher owns it.
                _logger.LogInformation(
                    "Job {JobId} is scheduled for {ScheduledAt:O}; the scheduled-job dispatcher owns it. Skipping.",
                    jobId,
                    job.ScheduledAt);
                return;

            case JobStatus.Pending:
                break;

            default:
                throw new InvalidOperationException($"Unexpected job status '{job.Status}' for job {jobId}.");
        }

        // 0 for the first delivery; increases with every MassTransit retry (phase 12).
        var retryAttempt = context.GetRetryAttempt();

        // Claim the job through the phase 20 state machine: Pending -> Processing.
        // Redeliveries owned by this worker are already Processing and skip the claim.
        if (job.Status == JobStatus.Pending)
        {
            job.TransitionTo(JobStatus.Processing);
            job.StartedAt = DateTime.UtcNow;
            job.WorkerId = workerId;
        }

        // Max() keeps the counter correct when a message is redelivered after a crash
        // (retryAttempt resets to 0 while job.Attempts already advanced).
        job.Attempts = Math.Max(job.Attempts, retryAttempt) + 1;

        // Heartbeat claim: lets the phase 14 recovery service detect this job as stuck
        // if this worker dies mid-execution.
        job.LastHeartbeatAt = DateTime.UtcNow;

        // Phase 19: record this execution in the job's history. Saved together with the
        // claim below; later finalized with CompletedAt/FailedAt/Error on the outcome.
        var attempt = new JobAttempt
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            AttemptNumber = job.Attempts,
            StartedAt = DateTime.UtcNow,
            WorkerId = workerId
        };
        job.JobAttempts.Add(attempt);

        var handler = _handlerResolver.Resolve(job.Type);
        if (handler is null)
        {
            var error = $"No handler registered for job type '{job.Type}'.";
            _logger.LogError("Job {JobId} failed permanently: {Error}", jobId, error);

            attempt.FailedAt = DateTime.UtcNow;
            attempt.Error = error;

            job.TransitionTo(JobStatus.Failed);
            job.FailedAt = DateTime.UtcNow;
            job.LastError = error;
            if (!await TrySaveClaimAsync(jobId, job, cancellationToken))
            {
                return;
            }

            JobMetrics.JobsProcessed.WithLabels(job.Type, "failed").Inc();
            return;
        }

        if (!await TrySaveClaimAsync(jobId, job, cancellationToken))
        {
            return;
        }

        // Phase 15: count every processing attempt (first attempts + retries).
        JobMetrics.JobAttempts.WithLabels(job.Type).Inc();

        var startedAt = DateTime.UtcNow;

        // Phase 23: structured logging scope for the entire job execution, carrying JobId,
        // CorrelationId, and AttemptNumber so every log entry is traceable end to end.
        using var loggingScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["JobId"] = jobId,
            ["CorrelationId"] = job.CorrelationId,
            ["AttemptNumber"] = job.Attempts,
            ["WorkerId"] = workerId
        });

        _logger.LogInformation(
            "Processing job {JobId} of type {JobType} (attempt {AttemptNumber}/{MaxAttempts}, worker {WorkerId}, correlation {CorrelationId}).",
            jobId,
            job.Type,
            job.Attempts,
            job.MaxAttempts,
            workerId,
            job.CorrelationId);

        // Refreshes LastHeartbeatAt in the background while the handler runs, and
        // watches for an operator cancellation (phase 16): when the API flips the job
        // to Cancelled, the poller cancels executionCts and the handler cooperates.
        using var executionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await using var heartbeat = new JobHeartbeatRefresher(
            _scopeFactory,
            jobId,
            _options.Value.HeartbeatInterval,
            _logger,
            cancellationToken,
            onCancellationRequested: () => executionCts.Cancel());

        try
        {
            var executionContext = new JobExecutionContext(job, ParsePayload(job.Payload));
            await handler.HandleAsync(executionContext, executionCts.Token);

            // Stop the heartbeat before persisting the final state: every tick bumps the
            // row's RowVersion, so stop ticking first and resync the tracked entity.
            await heartbeat.StopAsync();
            await _jobRepository.RefreshAsync(job, cancellationToken);

            if (job.Status == JobStatus.Cancelled)
            {
                // An operator cancelled the job just as the handler finished; the
                // cancellation wins and the message is acknowledged.
                attempt.Error = "Cancelled by an operator request; the completed handler result was discarded.";
                await _jobRepository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Job {JobId} was cancelled while completing; leaving it Cancelled.",
                    jobId);
                return;
            }

            if (job.Status != JobStatus.Processing || job.WorkerId != workerId)
            {
                // Phase 21: ownership lost (e.g. stuck-job recovery re-dispatched the job).
                // The winning writer owns the row now; never overwrite its newer state.
                _logger.LogWarning(
                    "Job {JobId} is no longer owned by this worker (status {JobStatus}, owner {OwnerWorkerId}); discarding the completed result.",
                    jobId,
                    job.Status,
                    job.WorkerId);
                return;
            }

            attempt.CompletedAt = DateTime.UtcNow;

            job.TransitionTo(JobStatus.Completed);
            job.CompletedAt = DateTime.UtcNow;
            await _jobRepository.SaveChangesAsync(cancellationToken);

            // Phase 15: record a successful completion and its duration.
            JobMetrics.JobsProcessed.WithLabels(job.Type, "completed").Inc();
            JobMetrics.JobDuration.WithLabels(job.Type)
                .Observe((DateTime.UtcNow - startedAt).TotalSeconds);

            _logger.LogInformation(
                "Job {JobId} of type {JobType} completed in {DurationMs} ms.",
                jobId,
                job.Type,
                (job.CompletedAt.Value - startedAt).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host shutdown in flight: leave the job in Processing (the attempt row stays
            // open) and rethrow so the message is redelivered to another worker.
            _logger.LogWarning(
                "Job {JobId} was interrupted by worker shutdown; the message will be redelivered.",
                jobId);
            throw;
        }
        catch (Exception ex)
        {
            // Stop the heartbeat before persisting the failure state: every tick bumps
            // the row's RowVersion, so stop ticking first and resync the tracked entity.
            await heartbeat.StopAsync();
            await _jobRepository.RefreshAsync(job, cancellationToken);

            if (job.Status == JobStatus.Cancelled)
            {
                // Phase 16: the exception came from the cooperative cancellation of an
                // operator-cancelled job. Leave it Cancelled and acknowledge the message.
                attempt.Error = "Cancelled by an operator request while running.";
                await _jobRepository.SaveChangesAsync(cancellationToken);

                JobMetrics.JobsProcessed.WithLabels(job.Type, "cancelled").Inc();
                _logger.LogInformation(
                    "Job {JobId} of type {JobType} was cancelled by an operator request.",
                    jobId,
                    job.Type);
                return;
            }

            if (job.Status != JobStatus.Processing || job.WorkerId != workerId)
            {
                // Phase 21: ownership lost (e.g. stuck-job recovery re-dispatched the job).
                // Leave the row to its current owner and acknowledge the message.
                _logger.LogWarning(
                    "Job {JobId} is no longer owned by this worker (status {JobStatus}, owner {OwnerWorkerId}); discarding the failure.",
                    jobId,
                    job.Status,
                    job.WorkerId);
                return;
            }

            attempt.FailedAt = DateTime.UtcNow;
            attempt.Error = ex.Message;
            job.LastError = ex.Message;

            var isTransient = _errorClassifier.IsTransient(ex);
            var hasAttemptsLeft = job.Attempts < job.MaxAttempts;

            if (isTransient && hasAttemptsLeft)
            {
                // Persist the failed attempt, then let the MassTransit retry policy
                // redeliver with exponential backoff (phases 12/13). The heartbeat is
                // refreshed too: while waiting out the backoff nothing executes, and the
                // phase 14 recovery must not reclaim a job that is mid-retry. This
                // requires HeartbeatTimeoutSeconds to exceed the largest retry delay.
                job.LastHeartbeatAt = DateTime.UtcNow;
                await _jobRepository.SaveChangesAsync(cancellationToken);

                _logger.LogWarning(
                    ex,
                    "Job {JobId} of type {JobType} failed transiently on attempt {AttemptNumber}/{MaxAttempts}; it will be retried.",
                    jobId,
                    job.Type,
                    job.Attempts,
                    job.MaxAttempts);
                throw;
            }

            if (isTransient)
            {
                // Retries exhausted: dead-letter the job (phase 13) and rethrow so
                // MassTransit moves the message to the _error queue (RabbitMQ DLQ).
                job.TransitionTo(JobStatus.DeadLettered);
                job.DeadLetteredAt = DateTime.UtcNow;
                await _jobRepository.SaveChangesAsync(cancellationToken);

                JobMetrics.JobsProcessed.WithLabels(job.Type, "dead_lettered").Inc();

                _logger.LogError(
                    ex,
                    "Job {JobId} of type {JobType} failed on its final attempt {AttemptNumber}/{MaxAttempts}; dead-lettering it.",
                    jobId,
                    job.Type,
                    job.Attempts,
                    job.MaxAttempts);
                throw;
            }

            // Permanent error: fail immediately without retrying or dead-lettering.
            job.TransitionTo(JobStatus.Failed);
            job.FailedAt = DateTime.UtcNow;
            await _jobRepository.SaveChangesAsync(cancellationToken);

            JobMetrics.JobsProcessed.WithLabels(job.Type, "failed").Inc();

            _logger.LogError(
                ex,
                "Job {JobId} of type {JobType} failed permanently on attempt {AttemptNumber} and will not be retried.",
                jobId,
                job.Type,
                job.Attempts);
        }
    }

    /// <summary>
    /// Saves the claim write and detects optimistic concurrency conflicts (phase 21):
    /// when another writer (another worker's claim, a cancellation request, the stuck-job
    /// recovery) updated the row since it was loaded, the rowversion check fails. The
    /// losing worker defers to the winner and acknowledges the message instead of
    /// silently overwriting the newer state.
    /// </summary>
    private async Task<bool> TrySaveClaimAsync(Guid jobId, Job job, CancellationToken cancellationToken)
    {
        try
        {
            await _jobRepository.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            var current = await _jobRepository.GetByIdAsync(jobId, cancellationToken);
            _logger.LogInformation(
                "Job {JobId} was updated concurrently during claim (now {JobStatus}); another writer won the race, skipping this delivery.",
                jobId,
                current?.Status);
            return false;
        }
    }

    private static JsonElement ParsePayload(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Payload should always be valid JSON; fall back to an empty object so the
            // handler's own validation surfaces the problem.
            return JsonSerializer.SerializeToElement(new { });
        }
    }
}
