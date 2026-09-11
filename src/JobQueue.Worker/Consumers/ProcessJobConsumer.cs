using JobQueue.Application.Abstractions;
using JobQueue.Domain.Enums;
using JobQueue.Infrastructure.Messaging.Contracts;
using MassTransit;

namespace JobQueue.Worker.Consumers;

/// <summary>
/// Consumes <see cref="ProcessJob"/> messages and processes the referenced job:
/// load from SQL Server, validate the current status, mark Processing, execute,
/// and mark Completed.
/// </summary>
/// <remarks>
/// RabbitMQ provides at-least-once delivery, so the same message may arrive more than
/// once. Only jobs in <see cref="JobStatus.Pending"/> are executed; every other status
/// means the work was already done, is owned by another worker, or must not run anymore.
/// </remarks>
public sealed class ProcessJobConsumer : IConsumer<ProcessJob>
{
    private readonly IJobRepository _jobRepository;
    private readonly ILogger<ProcessJobConsumer> _logger;

    public ProcessJobConsumer(IJobRepository jobRepository, ILogger<ProcessJobConsumer> logger)
    {
        _jobRepository = jobRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProcessJob> context)
    {
        var jobId = context.Message.JobId;
        var cancellationToken = context.CancellationToken;

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

            case JobStatus.Processing:
                _logger.LogWarning(
                    "Job {JobId} is already being processed by worker {WorkerId}; skipping duplicate delivery.",
                    jobId,
                    job.WorkerId);
                return;

            case JobStatus.Scheduled:
                _logger.LogInformation(
                    "Job {JobId} is scheduled for {ScheduledAt:O}; it will be dispatched by the scheduler (later phase). Skipping.",
                    jobId,
                    job.ScheduledAt);
                return;

            case JobStatus.Pending:
                break;

            default:
                throw new InvalidOperationException($"Unexpected job status '{job.Status}' for job {jobId}.");
        }

        var workerId = $"{Environment.MachineName}-{Environment.ProcessId}";

        job.Status = JobStatus.Processing;
        job.StartedAt = DateTime.UtcNow;
        job.WorkerId = workerId;
        job.Attempts += 1;
        await _jobRepository.SaveChangesAsync(cancellationToken);

        var startedAt = DateTime.UtcNow;
        _logger.LogInformation(
            "Processing job {JobId} of type {JobType} (attempt {AttemptNumber}/{MaxAttempts}, worker {WorkerId}, correlation {CorrelationId}).",
            jobId,
            job.Type,
            job.Attempts,
            job.MaxAttempts,
            workerId,
            job.CorrelationId);

        // Phase 8 executes a simulated unit of work. Phase 9 replaces this with the
        // IJobHandler abstraction resolved by Job.Type.
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

        job.Status = JobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        await _jobRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Job {JobId} of type {JobType} completed in {DurationMs} ms.",
            jobId,
            job.Type,
            (job.CompletedAt.Value - startedAt).TotalMilliseconds);
    }
}
