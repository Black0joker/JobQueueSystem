using JobQueue.Application.Abstractions;
using JobQueue.Domain.Enums;
using JobQueue.Domain.Exceptions;
using JobQueue.Domain.Jobs;

namespace JobQueue.Application.Jobs.Commands;

/// <summary>
/// Creates a job, persists it, and enqueues it for asynchronous processing.
/// The HTTP/API layer never executes the actual work.
/// </summary>
public sealed class CreateJobCommandHandler
{
    private readonly IJobRepository _jobRepository;
    private readonly IJobPublisher _jobPublisher;

    public CreateJobCommandHandler(IJobRepository jobRepository, IJobPublisher jobPublisher)
    {
        _jobRepository = jobRepository;
        _jobPublisher = jobPublisher;
    }

    public async Task<CreateJobResult> HandleAsync(
        CreateJobCommand command,
        CancellationToken cancellationToken = default)
    {
        // Idempotency: return the existing job when the key was already used.
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var existing = await _jobRepository.GetByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return new CreateJobResult(existing, AlreadyExisted: true);
            }
        }

        var job = Job.Create(
            command.Type,
            command.Payload,
            command.Priority,
            command.MaxAttempts,
            command.ScheduledAt,
            command.IdempotencyKey,
            command.CorrelationId);

        try
        {
            await _jobRepository.AddAsync(job, cancellationToken);
            await _jobRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateJobException)
        {
            // A concurrent request with the same idempotency key won the race.
            var existing = await _jobRepository.GetByIdempotencyKeyAsync(command.IdempotencyKey!, cancellationToken);
            if (existing is not null)
            {
                return new CreateJobResult(existing, AlreadyExisted: true);
            }

            throw;
        }

        // Only immediately dispatchable jobs are enqueued here; scheduled jobs are
        // dispatched once due by the worker's scheduled-job dispatcher (phase 17).
        if (job.Status == JobStatus.Pending)
        {
            await _jobPublisher.PublishAsync(job.Id, job.Type, cancellationToken);
        }

        return new CreateJobResult(job, AlreadyExisted: false);
    }
}
