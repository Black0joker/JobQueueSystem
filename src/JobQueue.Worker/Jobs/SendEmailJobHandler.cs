using System.Collections.Concurrent;
using System.Text.Json;
using JobQueue.Application.Abstractions;
using JobQueue.Domain.Jobs;

namespace JobQueue.Worker.Jobs;

/// <summary>
/// Simulates sending an email. Expects a payload containing "to" (and optionally
/// "subject"); a missing recipient is a permanent payload error.
/// </summary>
/// <remarks>
/// Failure-scenario test hooks (used by the phase 13/14 verifications and reusable
/// for phase 16 failure testing):
/// <list type="bullet">
/// <item>"failAttempts": N - the first N executions throw a TimeoutException (transient).</item>
/// <item>"workSeconds": S with "slowAttempts": N - the first N executions take S seconds,
/// simulating a long-running job; later executions use the normal 2-second delay.</item>
/// </list>
/// </remarks>
public sealed class SendEmailJobHandler : IJobHandler
{
    private static readonly TimeSpan DefaultWorkDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Counts executions per job so simulated failures/delays apply only to the first
    /// N attempts. Survives in-process retries; a restarted worker starts fresh.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, int> SimulatedExecutions = new();

    private readonly ILogger<SendEmailJobHandler> _logger;

    public SendEmailJobHandler(ILogger<SendEmailJobHandler> logger)
    {
        _logger = logger;
    }

    public string JobType => JobTypes.SendEmail;

    public async Task HandleAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        var to = context.Payload.TryGetProperty("to", out var toElement) ? toElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(to))
        {
            throw new PermanentJobException("The SendEmail payload is missing the required 'to' property.");
        }

        var executionNumber = SimulatedExecutions.AddOrUpdate(context.Job.Id, 1, (_, count) => count + 1);

        FailIfRequested(context, executionNumber);

        var subject = context.Payload.TryGetProperty("subject", out var subjectElement)
            ? subjectElement.GetString()
            : null;

        // Simulated SMTP round-trip.
        await Task.Delay(ResolveWorkDuration(context, executionNumber), cancellationToken);

        _logger.LogInformation(
            "Job {JobId}: simulated email sent to {To} (subject: {Subject}).",
            context.Job.Id,
            to,
            subject ?? "(none)");
    }

    private static void FailIfRequested(JobExecutionContext context, int executionNumber)
    {
        var failAttempts = ReadInt(context.Payload, "failAttempts");
        if (failAttempts > 0 && executionNumber <= failAttempts)
        {
            throw new TimeoutException(
                $"Simulated transient failure {executionNumber}/{failAttempts} for job {context.Job.Id}.");
        }
    }

    private static TimeSpan ResolveWorkDuration(JobExecutionContext context, int executionNumber)
    {
        var workSeconds = ReadInt(context.Payload, "workSeconds");
        var slowAttempts = ReadInt(context.Payload, "slowAttempts");

        if (workSeconds > 0 && slowAttempts > 0 && executionNumber <= slowAttempts)
        {
            return TimeSpan.FromSeconds(workSeconds);
        }

        return DefaultWorkDuration;
    }

    private static int ReadInt(JsonElement payload, string propertyName)
        => payload.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.Number
            ? element.GetInt32()
            : 0;
}
