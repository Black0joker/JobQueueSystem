using JobQueue.Domain.Jobs;

namespace JobQueue.Application.Jobs.Commands;

/// <summary>
/// Result of <see cref="CreateJobCommand"/>.
/// </summary>
/// <param name="Job">The created job, or the pre-existing job when idempotency matched.</param>
/// <param name="AlreadyExisted">True when an existing job was returned instead of creating a new one.</param>
public sealed record CreateJobResult(Job Job, bool AlreadyExisted);
