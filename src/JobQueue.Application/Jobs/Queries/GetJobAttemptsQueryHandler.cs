using JobQueue.Application.Abstractions;
using JobQueue.Domain.Jobs;

namespace JobQueue.Application.Jobs.Queries;

/// <summary>
/// Loads the execution history of a job: one entry per processing attempt (phase 19).
/// </summary>
public sealed class GetJobAttemptsQueryHandler
{
    private readonly IJobRepository _jobRepository;

    public GetJobAttemptsQueryHandler(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    /// <summary>
    /// Returns the job's attempts earliest-first, or null when the job does not exist.
    /// </summary>
    public async Task<IReadOnlyList<JobAttempt>?> HandleAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        return await _jobRepository.GetAttemptsAsync(jobId, cancellationToken);
    }
}
