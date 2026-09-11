namespace JobQueue.Application.Jobs.Commands;

/// <summary>
/// Requests a manual retry of a failed or dead-lettered job (phase 16).
/// </summary>
/// <param name="JobId">The identifier of the job to retry.</param>
public sealed record RetryJobCommand(Guid JobId);
