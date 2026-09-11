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
/// Failure-scenario test hook: a payload property "failAttempts" (integer) makes the
/// first N executions throw a <see cref="TimeoutException"/> (transient) before the
/// simulated send succeeds. Used to exercise the retry policy end to end.
/// </remarks>
public sealed class SendEmailJobHandler : IJobHandler
{
    /// <summary>Keeps the simulated failure count per job across retry attempts.</summary>
    private static readonly ConcurrentDictionary<Guid, int> SimulatedFailures = new();

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

        SimulateTransientFailures(context);

        var subject = context.Payload.TryGetProperty("subject", out var subjectElement)
            ? subjectElement.GetString()
            : null;

        // Simulated SMTP round-trip.
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

        _logger.LogInformation(
            "Job {JobId}: simulated email sent to {To} (subject: {Subject}).",
            context.Job.Id,
            to,
            subject ?? "(none)");
    }

    private static void SimulateTransientFailures(JobExecutionContext context)
    {
        var failAttempts = context.Payload.TryGetProperty("failAttempts", out var element)
            && element.ValueKind == JsonValueKind.Number
                ? element.GetInt32()
                : 0;

        if (failAttempts <= 0)
        {
            return;
        }

        var failuresSoFar = SimulatedFailures.AddOrUpdate(context.Job.Id, 1, (_, count) => count + 1);
        if (failuresSoFar <= failAttempts)
        {
            throw new TimeoutException(
                $"Simulated transient failure {failuresSoFar}/{failAttempts} for job {context.Job.Id}.");
        }
    }
}
