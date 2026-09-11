using JobQueue.Domain.Enums;

namespace JobQueue.Application.Jobs.Queries;

/// <summary>
/// Query to list jobs with optional filtering and pagination.
/// </summary>
/// <param name="Status">Optional status filter (e.g. Failed, Pending).</param>
/// <param name="Type">Optional job type filter (e.g. "SendEmail").</param>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
public sealed record ListJobsQuery(
    JobStatus? Status = null,
    string? Type = null,
    int Page = 1,
    int PageSize = 20);
