using System.Text.Json;
using JobQueue.Domain.Jobs;

namespace JobQueue.Application.Abstractions;

/// <summary>
/// The execution input passed to an <see cref="IJobHandler"/>: the job entity and its
/// parsed JSON payload.
/// </summary>
/// <param name="Job">The job being executed.</param>
/// <param name="Payload">The job payload parsed as a <see cref="JsonElement"/>.</param>
public sealed record JobExecutionContext(Job Job);
