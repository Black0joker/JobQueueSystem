using JobQueue.Application.Abstractions;
using JobQueue.Domain.Jobs;

namespace JobQueue.Application.Jobs.Queries;

/// <summary>
/// Loads a single job by its identifier.
/// </summary>
public sealed class GetJobByIdQueryHandler
{
    private readonly IJobRepository _jobRepository;

    public GetJobByIdQueryHandler(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    /// <summary>Returns the job with the given identifier, or null when it does not exist.</summary>
    public Task<Job?> HandleAsync(Guid id, CancellationToken cancellationToken = default)
        => _jobRepository.GetByIdAsync(id, cancellationToken);
}
