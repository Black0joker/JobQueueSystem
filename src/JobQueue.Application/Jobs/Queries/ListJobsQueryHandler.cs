using JobQueue.Application.Abstractions;
using JobQueue.Domain.Jobs;

namespace JobQueue.Application.Jobs.Queries;

/// <summary>
/// Lists jobs with optional status/type filtering and pagination.
/// </summary>
public sealed class ListJobsQueryHandler
{
    /// <summary>Default number of items per page.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Maximum number of items a single page may contain.</summary>
    public const int MaxPageSize = 100;

    private readonly IJobRepository _jobRepository;

    public ListJobsQueryHandler(IJobRepository jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public Task<(IReadOnlyList<Job> Items, int TotalCount)> HandleAsync(
        ListJobsQuery query,
        CancellationToken cancellationToken = default)
    {
        // Defensive normalization; the API layer validates these values as well.
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var type = string.IsNullOrWhiteSpace(query.Type) ? null : query.Type.Trim();

        return _jobRepository.ListAsync(query.Status, type, page, pageSize, cancellationToken);
    }
}
