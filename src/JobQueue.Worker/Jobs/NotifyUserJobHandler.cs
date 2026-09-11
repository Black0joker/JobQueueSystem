using JobQueue.Application.Abstractions;
using JobQueue.Domain.Jobs;

namespace JobQueue.Worker.Jobs;

/// <summary>
/// Simulates an external HTTP/API operation: pushing a notification to a user.
/// </summary>
public sealed class NotifyUserJobHandler : IJobHandler
{
    private readonly ILogger<NotifyUserJobHandler> _logger;

    public NotifyUserJobHandler(ILogger<NotifyUserJobHandler> logger)
    {
        _logger = logger;
    }

    public string JobType => JobTypes.NotifyUser;

    public async Task HandleAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        var userId = context.Payload.TryGetProperty("userId", out var userIdElement)
            ? userIdElement.ToString()
            : null;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new PermanentJobException("The NotifyUser payload is missing the required 'userId' property.");
        }

        // Simulated outbound HTTP call to a notification service.
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

        _logger.LogInformation(
            "Job {JobId}: simulated notification delivered to user {UserId}.",
            context.Job.Id,
            userId);
    }
}
