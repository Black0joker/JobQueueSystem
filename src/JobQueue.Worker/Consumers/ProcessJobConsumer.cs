using System.Text.Json;
using JobQueue.Application.Abstractions;
using JobQueue.Domain.Enums;
using JobQueue.Infrastructure.Messaging.Contracts;
using MassTransit;

namespace JobQueue.Worker.Consumers;

/// <summary>
/// Consumes <see cref="ProcessJob"/> messages and processes the referenced job:
/// load from SQL Server, validate the current status, mark Processing, resolve and
/// execute the matching <see cref="IJobHandler"/>, then mark Completed or Failed.
/// </summary>
/// <remarks>
/// RabbitMQ provides at-least-once delivery, so the same message may arrive more than
/// once. Only jobs in <see cref="JobStatus.Pending"/> are executed; every other status
/// means the work was already done, is owned by another worker, or must not run anymore.
/// </remarks>
public sealed class ProcessJobConsumer : IConsumer<ProcessJob>
{
    private readonly IJobRepository _jobRepository;
    private readonly IJobHandlerResolver _handlerResolver;
    private readonly ILogger<ProcessJobConsumer> _logger;

    public ProcessJobConsumer(
        IJobRepository jobRepository,
        IJobHandlerResolver handlerResolver,
        ILogger<ProcessJobConsumer> logger)
    {
        _jobRepository = jobRepository;
        _handlerResolver = handlerResolver;
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

        var handler = _handlerResolver.Resolve(job.Type);
        if (handler is null)
        {
            var error = $"No handler registered for job type '{job.Type}'.";
            _logger.LogError("Job {JobId} failed permanently: {Error}", jobId, error);

            job.Status = JobStatus.Failed;
            job.FailedAt = DateTime.UtcNow;
            job.LastError = error;
            await _jobRepository.SaveChangesAsync(cancellationToken);
            return;
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

        try
        {
            var executionContext = new JobExecutionContext(job, ParsePayload(job.Payload));
            await handler.HandleAsync(executionContext, cancellationToken);

            job.Status = JobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            await _jobRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Job {JobId} of type {JobType} completed in {DurationMs} ms.",
                jobId,
                job.Type,
                (job.CompletedAt.Value - startedAt).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host shutdown in flight: leave the job in Processing and rethrow so the
            // message is redelivered to another worker (graceful shutdown, later phase).
            _logger.LogWarning(
                "Job {JobId} was interrupted by worker shutdown; the message will be redelivered.",
                jobId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Job {JobId} of type {JobType} failed on attempt {AttemptNumber}.",
                jobId,
                job.Type,
                job.Attempts);

            job.Status = JobStatus.Failed;
            job.FailedAt = DateTime.UtcNow;
            job.LastError = ex.Message;
            await _jobRepository.SaveChangesAsync(cancellationToken);

            // Retry, exponential backoff, and dead-letter handling arrive in phases 11-13.
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
