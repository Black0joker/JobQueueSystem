namespace JobQueue.Application.Jobs.Commands;

/// <summary>
/// Requests cancellation of a job (phase 16).
/// </summary>
/// <param name="JobId">The identifier of the job to cancel.</param>
public sealed record CancelJobCommand(Guid JobId);
