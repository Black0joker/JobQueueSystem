using System.Text.Json;
using JobQueue.Api.Contracts;
using JobQueue.Application.Jobs.Commands;
using JobQueue.Domain.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace JobQueue.Api.Controllers;

/// <summary>
/// Job lifecycle endpoints. Job creation only registers and enqueues work;
/// the actual processing happens asynchronously in the workers.
/// </summary>
[ApiController]
[Route("jobs")]
public class JobsController : ControllerBase
{
    private readonly CreateJobCommandHandler _createJobCommandHandler;

    public JobsController(CreateJobCommandHandler createJobCommandHandler)
    {
        _createJobCommandHandler = createJobCommandHandler;
    }

    /// <summary>
    /// Creates a new job and enqueues it for asynchronous processing.
    /// </summary>
    /// <response code="202">The job was accepted for processing.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreateJobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateJobResponse>> Create(
        [FromBody] CreateJobRequest request,
        CancellationToken cancellationToken)
    {
        var payload = request.Payload is { ValueKind: JsonValueKind.Object or JsonValueKind.Array } element
            ? element.GetRawText()
            : "{}";

        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        var correlationId = Guid.TryParse(Request.Headers["X-Correlation-Id"].FirstOrDefault(), out var parsed)
            ? parsed
            : Guid.NewGuid();

        var command = new CreateJobCommand(
            Type: request.Type,
            Payload: payload,
            Priority: request.Priority ?? 0,
            MaxAttempts: request.MaxAttempts ?? Job.DefaultMaxAttempts,
            ScheduledAt: request.ScheduledAt,
            IdempotencyKey: idempotencyKey,
            CorrelationId: correlationId);

        var result = await _createJobCommandHandler.HandleAsync(command, cancellationToken);

        return Accepted(new CreateJobResponse(result.Job.Id, result.Job.Status.ToString()));
    }
}
