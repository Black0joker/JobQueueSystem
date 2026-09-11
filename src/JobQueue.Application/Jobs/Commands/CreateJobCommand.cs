using JobQueue.Domain.Jobs;

namespace JobQueue.Application.Jobs.Commands;

/// <summary>
/// Command to create and enqueue a new job.
/// </summary>
/// <param name="Type">Logical job type used to resolve the handler (e.g. "SendEmail").</param>
/// <param name="Payload">JSON payload describing the job's input data.</param>
/// <param name="Priority">Dispatch priority (higher values first).</param>
/// <param name="MaxAttempts">Maximum attempts before the job is dead-lettered.</param>
/// <param name="ScheduledAt">Optional future UTC execution time; the job starts as Scheduled.</param>
/// <param name="IdempotencyKey">Optional key preventing duplicate job creation.</param>
/// <param name="CorrelationId">Optional correlation identifier for end-to-end tracing.</param>
public sealed record CreateJobCommand(
    string Type,
    string Payload,
    int Priority = 0,
    int MaxAttempts = Job.DefaultMaxAttempts,
    DateTime? ScheduledAt = null,
    string? IdempotencyKey = null,
    Guid? CorrelationId = null);
