using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using JobQueue.Api.Contracts;
using JobQueue.Api.Observability;
using JobQueue.Application.Jobs.Commands;
using JobQueue.Application.Jobs.Queries;
using JobQueue.Domain.Enums;
using JobQueue.Domain.Exceptions;
using JobQueue.Domain.Jobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    private readonly RetryJobCommandHandler _retryJobCommandHandler;
    private readonly CancelJobCommandHandler _cancelJobCommandHandler;
    private readonly GetJobByIdQueryHandler _getJobByIdQueryHandler;
    private readonly GetJobAttemptsQueryHandler _getJobAttemptsQueryHandler;
    private readonly ListJobsQueryHandler _listJobsQueryHandler;

    public JobsController(
        CreateJobCommandHandler createJobCommandHandler,
        RetryJobCommandHandler retryJobCommandHandler,
        CancelJobCommandHandler cancelJobCommandHandler,
        GetJobByIdQueryHandler getJobByIdQueryHandler,
        GetJobAttemptsQueryHandler getJobAttemptsQueryHandler,
        ListJobsQueryHandler listJobsQueryHandler)
    {
        _createJobCommandHandler = createJobCommandHandler;
        _retryJobCommandHandler = retryJobCommandHandler;
        _cancelJobCommandHandler = cancelJobCommandHandler;
        _getJobByIdQueryHandler = getJobByIdQueryHandler;
        _getJobAttemptsQueryHandler = getJobAttemptsQueryHandler;
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

        if (!result.AlreadyExisted)
        {
            // Phase 15: count newly created jobs (idempotent hits are not new work).
            ApiMetrics.JobsCreated.WithLabels(result.Job.Type).Inc();
        }
        else
        {
            // Phase 18: an idempotent replay returns the original job, unchanged.
            Response.Headers["Idempotent-Replay"] = "true";
        }

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

    /// <summary>
    /// Returns the execution history of a job: one entry per processing attempt,
    /// earliest first (phase 19).
    /// </summary>
    /// <response code="200">The job's attempts.</response>
    /// <response code="404">No job exists with the given identifier.</response>
    [HttpGet("{id:guid}/attempts")]
    [ProducesResponseType(typeof(IReadOnlyList<JobAttemptResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<JobAttemptResponse>>> GetAttempts(
        Guid id,
        CancellationToken cancellationToken)
    {
        var attempts = await _getJobAttemptsQueryHandler.HandleAsync(id, cancellationToken);
        if (attempts is null)
        {
            return NotFound();
        }

        return Ok(attempts.Select(JobAttemptResponse.FromAttempt).ToList());
    }

    /// <summary>
    /// Manually retries a failed or dead-lettered job: resets its retry state,
    /// returns it to Pending, and re-enqueues it. The original job is reused.
    /// </summary>
    /// <response code="200">The job was reset to Pending and re-enqueued.</response>
    /// <response code="404">No job exists with the given identifier.</response>
    /// <response code="409">The job is in a state that cannot be retried.</response>
    [HttpPost("{id:guid}/retry")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobResponse>> Retry(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var job = await _retryJobCommandHandler.HandleAsync(new RetryJobCommand(id), cancellationToken);

            ApiMetrics.JobsRetried.Inc();

            return Ok(JobResponse.FromJob(job));
        }
        catch (JobNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidJobTransitionException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Cancels a pending, scheduled, or processing job. Pending and scheduled jobs are
    /// cancelled immediately; a processing job is cancelled cooperatively - the owning
    /// worker observes the cancellation and stops the running handler. Terminal states
    /// cannot be cancelled.
    /// </summary>
    /// <response code="200">The job was cancelled (or the cancellation signal was stored).</response>
    /// <response code="404">No job exists with the given identifier.</response>
    /// <response code="409">The job is in a state that cannot be cancelled, or a concurrent update won the race.</response>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var job = await _cancelJobCommandHandler.HandleAsync(new CancelJobCommand(id), cancellationToken);

            ApiMetrics.JobsCancelled.Inc();

            return Ok(JobResponse.FromJob(job));
        }
        catch (DbUpdateConcurrencyException)
        {
            // A heartbeat or worker save bumped the row between read and write; retry
            // once with fresh state before surfacing the conflict.
            try
            {
                var job = await _cancelJobCommandHandler.HandleAsync(new CancelJobCommand(id), cancellationToken);

                ApiMetrics.JobsCancelled.Inc();

                return Ok(JobResponse.FromJob(job));
            }
            catch (JobNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidJobTransitionException ex)
            {
                return Conflict(new { error = ex.Message });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new
                {
                    error = $"Job {id} is being updated concurrently; please retry the cancellation."
                });
            }
        }
        catch (JobNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidJobTransitionException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
