using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace JobQueue.Api.Contracts;

/// <summary>
/// Request body for POST /jobs.
/// </summary>
public sealed record CreateJobRequest
{
    /// <summary>Logical job type used to resolve the handler (e.g. "SendEmail").</summary>
    [Required]
    [StringLength(200)]
    public string Type { get; init; } = default!;

    /// <summary>JSON payload describing the job's input data.</summary>
    public JsonElement? Payload { get; init; }

    /// <summary>Dispatch priority (higher values first). Defaults to 0.</summary>
    public int? Priority { get; init; }

    /// <summary>Maximum attempts before the job is dead-lettered. Defaults to 3.</summary>
    [Range(1, int.MaxValue)]
    public int? MaxAttempts { get; init; }

    /// <summary>Optional future UTC execution time; the job starts as Scheduled.</summary>
    public DateTime? ScheduledAt { get; init; }
}
