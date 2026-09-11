using System.Text.Json;
using JobQueue.Domain.Jobs;

namespace JobQueue.Api.Contracts;

/// <summary>
/// Read model describing a job's current state (GET /jobs/{id}, GET /jobs).
/// </summary>
public sealed record JobResponse
{
    public Guid Id { get; init; }

    public string Type { get; init; } = default!;

    public string Status { get; init; } = default!;

    public int Priority { get; init; }

    public int Attempts { get; init; }

    public int MaxAttempts { get; init; }

    public JsonElement? Payload { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? ScheduledAt { get; init; }

    public DateTime? StartedAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public DateTime? FailedAt { get; init; }

    public DateTime? CancelledAt { get; init; }

    public string? LastError { get; init; }

    public string? IdempotencyKey { get; init; }

    public Guid? CorrelationId { get; init; }

    public string? WorkerId { get; init; }

    /// <summary>Maps a domain job to the API read model.</summary>
    public static JobResponse FromJob(Job job) => new()
    {
        Id = job.Id,
        Type = job.Type,
        Status = job.Status.ToString(),
        Priority = job.Priority,
        Attempts = job.Attempts,
        MaxAttempts = job.MaxAttempts,
        Payload = ParsePayload(job.Payload),
        CreatedAt = job.CreatedAt,
        ScheduledAt = job.ScheduledAt,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt,
        FailedAt = job.FailedAt,
        CancelledAt = job.CancelledAt,
        LastError = job.LastError,
        IdempotencyKey = job.IdempotencyKey,
        CorrelationId = job.CorrelationId,
        WorkerId = job.WorkerId
    };

    private static JsonElement? ParsePayload(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // The payload should always be valid JSON; fall back to exposing the raw text.
            return JsonSerializer.SerializeToElement(payload);
        }
    }
}
