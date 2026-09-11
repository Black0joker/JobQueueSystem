using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using JobQueue.Api.Contracts;
using JobQueue.Application.Jobs.Commands;
using JobQueue.Application.Jobs.Queries;
using JobQueue.Domain.Enums;
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
    private readonly GetJobByIdQueryHandler _getJobByIdQueryHandler;
    private readonly ListJobsQueryHandler _listJobsQueryHandler;

    public JobsController(
        CreateJobCommandHandler createJobCommandHandler,
        GetJobByIdQueryHandler getJobByIdQueryHandler,
        ListJobsQueryHandler listJobsQueryHandler)
    {
        _createJobCommandHandler = createJobCommandHandler;
        _getJobByIdQueryHandler = getJobByIdQueryHandler;
        _listJobsQueryHandler = listJobsQueryHandler;
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

    /// <summary>
    /// Returns a single job by its identifier.
    /// </summary>
    /// <response code="200">The job was found.</response>
    /// <response code="404">No job exists with the given identifier.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var job = await _getJobByIdQueryHandler.HandleAsync(id, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return Ok(JobResponse.FromJob(job));
    }

    /// <summary>
    /// Lists jobs with optional filtering and pagination, newest first.
    /// </summary>
    /// <param name="status">Optional status filter (e.g. "Failed", "Pending").</param>
    /// <param name="type">Optional job type filter (e.g. "SendEmail").</param>
    /// <param name="page">One-based page number. Defaults to 1.</param>
    /// <param name="pageSize">Items per page (1-100). Defaults to 20.</param>
    /// <response code="200">The matching jobs.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedJobsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedJobsResponse>> List(
        [FromQuery] string? status,
        [FromQuery] string? type,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, ListJobsQueryHandler.MaxPageSize)] int pageSize = ListJobsQueryHandler.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        JobStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<JobStatus>(status, ignoreCase: true, out var jobStatus))
            {
                return BadRequest(new
                {
                    error = $"Invalid job status '{status}'. Valid values: {string.Join(", ", Enum.GetNames<JobStatus>())}."
                });
            }

            parsedStatus = jobStatus;
        }

        var query = new ListJobsQuery(
            Status: parsedStatus,
            Type: string.IsNullOrWhiteSpace(type) ? null : type.Trim(),
            Page: page,
            PageSize: pageSize);

        var (items, totalCount) = await _listJobsQueryHandler.HandleAsync(query, cancellationToken);

        var response = new PagedJobsResponse(
            Items: items.Select(JobResponse.FromJob).ToList(),
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize);

        return Ok(response);
    }
}
