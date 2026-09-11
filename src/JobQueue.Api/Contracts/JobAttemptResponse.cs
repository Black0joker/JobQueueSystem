using JobQueue.Domain.Jobs;

namespace JobQueue.Api.Contracts;

/// <summary>
/// One execution attempt of a job (phase 19): part of the job's execution history.
/// </summary>
/// <param name="AttemptId">Unique identifier of the attempt.</param>
/// <param name="AttemptNumber">One-based attempt number within the current retry cycle.</param>
/// <param name="StartedAt">UTC timestamp when the attempt started.</param>
/// <param name="CompletedAt">UTC timestamp when the attempt succeeded, if it did.</param>
/// <param name="FailedAt">UTC timestamp when the attempt failed, if it did.</param>
/// <param name="Error">Error message when the attempt failed or was cancelled.</param>
/// <param name="WorkerId">Identifier of the worker that executed the attempt.</param>
public sealed record JobAttemptResponse(
    Guid AttemptId,
    int AttemptNumber,
    DateTime StartedAt,
    DateTime? CompletedAt,
    DateTime? FailedAt,
    string? Error,
    string? WorkerId)
{
    /// <summary>Maps a domain attempt to its API representation.</summary>
    public static JobAttemptResponse FromAttempt(JobAttempt attempt)
        => new(
            attempt.Id,
            attempt.AttemptNumber,
            attempt.StartedAt,
            attempt.CompletedAt,
            attempt.FailedAt,
            attempt.Error,
            attempt.WorkerId);
}
