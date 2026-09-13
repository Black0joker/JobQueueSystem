using JobQueue.Application.Abstractions;
using JobQueue.Domain.Jobs;

namespace JobQueue.Worker.Jobs;

/// <summary>
/// Simulates database-oriented work: decrementing stock for the ordered items.
/// </summary>
public sealed class UpdateInventoryJobHandler : IJobHandler
{
    private readonly ILogger<UpdateInventoryJobHandler> _logger;

    public UpdateInventoryJobHandler(ILogger<UpdateInventoryJobHandler> logger)
    {
        _logger = logger;
    }

    public string JobType => JobTypes.UpdateInventory;

    public async Task HandleAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        var payload = JobParser.ParsePayload(context.Job.Payload);
        var orderId = payload.TryGetProperty("orderId", out var orderIdElement)
            ? orderIdElement.ToString()
            : null;
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new PermanentJobException("The UpdateInventory payload is missing the required 'orderId' property.");
        }

        // Simulated database transaction.
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

        _logger.LogInformation(
            "Job {JobId}: simulated inventory updated for order {OrderId}.",
            context.Job.Id,
            orderId);
    }
}
