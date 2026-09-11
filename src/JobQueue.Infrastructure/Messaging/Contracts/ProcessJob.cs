namespace JobQueue.Infrastructure.Messaging.Contracts;

/// <summary>
/// Message instructing a worker to process the job with the given identifier.
/// </summary>
/// <remarks>
/// The message carries only the job identifier (and type for routing/diagnostics)
/// rather than duplicating the full database record; workers load the authoritative
/// state from SQL Server.
/// </remarks>
public sealed record ProcessJob
{
    /// <summary>Identifier of the job to process.</summary>
    public Guid JobId { get; init; }

    /// <summary>The logical job type (e.g. "SendEmail").</summary>
    public string Type { get; init; } = default!;
}
