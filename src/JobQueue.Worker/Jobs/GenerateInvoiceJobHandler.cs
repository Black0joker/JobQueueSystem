using JobQueue.Application.Abstractions;
using JobQueue.Domain.Jobs;
using MassTransit;

namespace JobQueue.Worker.Jobs;

/// <summary>
/// Simulates CPU/file work: rendering an invoice document for an order.
/// </summary>
public sealed class GenerateInvoiceJobHandler : IJobHandler
{
    private readonly ILogger<GenerateInvoiceJobHandler> _logger;

    public GenerateInvoiceJobHandler(ILogger<GenerateInvoiceJobHandler> logger)
    {
        _logger = logger;
    }

    public string JobType => JobTypes.GenerateInvoice;

    public async Task HandleAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        var payload=JobParser.ParsePayload(context.Job.Payload);
        var orderId = payload.TryGetProperty("orderId", out var orderIdElement)
            ? orderIdElement.ToString()
            : null;
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new PermanentJobException("The GenerateInvoice payload is missing the required 'orderId' property.");
        }

        // Simulated document rendering + file write.
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

        _logger.LogInformation(
            "Job {JobId}: simulated invoice generated for order {OrderId} (invoice-{InvoiceFile}.pdf).",
            context.Job.Id,
            orderId,
            orderId);
    }
}
