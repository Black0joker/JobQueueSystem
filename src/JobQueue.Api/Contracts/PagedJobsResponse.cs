namespace JobQueue.Api.Contracts;

/// <summary>
/// Paginated job list response (GET /jobs).
/// </summary>
public sealed record PagedJobsResponse(
    IReadOnlyList<JobResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
