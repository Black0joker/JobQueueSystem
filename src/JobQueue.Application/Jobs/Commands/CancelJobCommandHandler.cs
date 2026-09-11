using JobQueue.Application.Abstractions;
using JobQueue.Domain.Enums;
using JobQueue.Domain.Exceptions;
using JobQueue.Domain.Jobs;

namespace JobQueue.Application.Jobs.Commands;

/// <summary>
/// Cancels a job (phase 16). Pending and scheduled jobs are cancelled immediately;
/// a processing job is marked cancelled in the database, which the owning worker
/// observes through its heartbeat/cancellation poller and translates into a
/// cooperative <see cref="CancellationToken"/> cancellation of the running handler.
/// Terminal states cannot be cancelled.
/// </summary>
public sealed class CancelJobCommandHandler
{
    private static readonly JobStatus[] CancellableStatuses =
        [JobStatus.Pending, JobStatus.Scheduled, JobStatus.Processing];

    private readonly IJobRepository _jobRepository;

    public CancelJobCommandHandler(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<Job> HandleAsync(CancelJobCommand command, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetTrackedByIdAsync(command.JobId, cancellationToken)
            ?? throw new JobNotFoundException(command.JobId);

        if (!CancellableStatuses.Contains(job.Status))
        {
            throw new InvalidJobTransitionException(
                $"Job {job.Id} cannot be cancelled from status '{job.Status}'. " +
                $"Only {string.Join(", ", CancellableStatuses)} jobs can be cancelled.");
        }

        // Pending/Scheduled/Processing -> Cancelled, enforced by the phase 20 state machine.
        job.TransitionTo(JobStatus.Cancelled);
        job.CancelledAt = DateTime.UtcNow;
        await _jobRepository.SaveChangesAsync(cancellationToken);

        return job;
    }
}
