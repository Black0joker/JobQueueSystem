using JobQueue.Application.Abstractions;
using JobQueue.Domain.Jobs;

namespace JobQueue.Worker.Jobs;

/// <summary>
/// Simulates sending an email. Expects a payload containing "to" (and optionally
/// "subject"); a missing recipient is a permanent payload error.
/// </summary>
public sealed class SendEmailJobHandler : IJobHandler
{
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
            throw new InvalidOperationException("The SendEmail payload is missing the required 'to' property.");
        }

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
}
