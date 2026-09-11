using JobQueue.Application.Abstractions;
using JobQueue.Domain.Enums;
using JobQueue.Domain.Exceptions;
using JobQueue.Domain.Jobs;

namespace JobQueue.Application.Jobs.Commands;

/// <summary>
/// Manually retries a failed or dead-lettered job (phase 16): resets the retry state,
/// returns the job to <see cref="JobStatus.Pending"/>, and publishes a new ProcessJob
/// message. The original job is reused — no duplicate job is created.
/// </summary>
public sealed class RetryJobCommandHandler
{
    private static readonly JobStatus[] RetryableStatuses = [JobStatus.Failed, JobStatus.DeadLettered];

    private readonly IJobRepository _jobRepository;
    private readonly IJobPublisher _jobPublisher;

    public RetryJobCommandHandler(IJobRepository jobRepository, IJobPublisher jobPublisher)
    {
        _jobRepository = jobRepository;
        _jobPublisher = jobPublisher;
    }

    public async Task<Job> HandleAsync(RetryJobCommand command, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetTrackedByIdAsync(command.JobId, cancellationToken)
            ?? throw new JobNotFoundException(command.JobId);

        if (!RetryableStatuses.Contains(job.Status))
        {
            throw new InvalidJobTransitionException(
                $"Job {job.Id} cannot be retried from status '{job.Status}'. " +
                $"Only {string.Join(" or ", RetryableStatuses)} jobs can be retried.");
        }

        // Reset the retry state so the job gets a fresh attempt budget. The transition
        // Failed/DeadLettered -> Pending is enforced by the phase 20 state machine.
        job.TransitionTo(JobStatus.Pending);
        job.Attempts = 0;
        job.LastError = null;
        job.FailedAt = null;
        job.DeadLetteredAt = null;
        job.StartedAt = null;
        job.WorkerId = null;
        job.LastHeartbeatAt = null;

        await _jobRepository.SaveChangesAsync(cancellationToken);

        // Re-dispatch: the worker picks the message up like any new job. The consumer's
        // status guard skips the message if the job is cancelled again before delivery.
        await _jobPublisher.PublishAsync(job.Id, job.Type, cancellationToken);

        return job;
    }
}
