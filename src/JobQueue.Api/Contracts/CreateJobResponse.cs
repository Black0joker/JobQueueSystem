namespace JobQueue.Api.Contracts;

/// <summary>
/// Response body for POST /jobs (202 Accepted).
/// </summary>
public sealed record CreateJobResponse(Guid Id, string Status);
