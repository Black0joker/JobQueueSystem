using JobQueue.Application.Abstractions;
using JobQueue.Application.Jobs.Commands;
using JobQueue.Domain.Enums;
using JobQueue.Domain.Exceptions;
using JobQueue.Domain.Jobs;

namespace JobQueue.UnitTests;

/// <summary>
/// Phase 16: manual retry and cancellation command rules.
/// </summary>
public class RetryAndCancelJobCommandHandlerTests
{
    private readonly FakeJobRepository _repository = new();
    private readonly FakeJobPublisher _publisher = new();

    [Fact]
    public async Task Retry_ResetsFailedJobToPending_AndPublishes()
    {
        var job = CreateJob(JobStatus.Failed);
        job.Attempts = 3;
        job.LastError = "boom";
        job.FailedAt = DateTime.UtcNow;
        job.WorkerId = "worker-1";
        _repository.Job = job;

        var result = await new RetryJobCommandHandler(_repository, _publisher)
            .HandleAsync(new RetryJobCommand(job.Id));

        Assert.Equal(JobStatus.Pending, result.Status);
        Assert.Equal(0, result.Attempts);
        Assert.Null(result.LastError);
        Assert.Null(result.FailedAt);
        Assert.Null(result.WorkerId);
        Assert.True(_repository.Saved);
        Assert.Equal((job.Id, job.Type), _publisher.Published);
    }

    [Fact]
    public async Task Retry_ResetsDeadLetteredJobToPending_AndPublishes()
    {
        var job = CreateJob(JobStatus.DeadLettered);
        job.Attempts = 3;
        job.DeadLetteredAt = DateTime.UtcNow;
        _repository.Job = job;

        var result = await new RetryJobCommandHandler(_repository, _publisher)
            .HandleAsync(new RetryJobCommand(job.Id));

        Assert.Equal(JobStatus.Pending, result.Status);
        Assert.Null(result.DeadLetteredAt);
        Assert.Equal((job.Id, job.Type), _publisher.Published);
    }

    [Fact]
    public async Task Retry_BuffersPublishBeforeSave_ForTransactionalOutbox()
    {
        // Phase 22: the publish must be buffered before the save so the state reset
        // and the outbox message commit in one transaction.
        var operations = new List<string>();
        var repository = new FakeJobRepository(operations) { Job = CreateJob(JobStatus.Failed) };
        var publisher = new FakeJobPublisher(operations);

        await new RetryJobCommandHandler(repository, publisher)
            .HandleAsync(new RetryJobCommand(repository.Job!.Id));

        var publishIndex = operations.IndexOf("publish");
        var saveIndex = operations.IndexOf("save");

        Assert.NotEqual(-1, publishIndex);
        Assert.NotEqual(-1, saveIndex);
        Assert.True(publishIndex < saveIndex);
    }

    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Scheduled)]
    [InlineData(JobStatus.Processing)]
    [InlineData(JobStatus.Completed)]
    [InlineData(JobStatus.Cancelled)]
    public async Task Retry_Throws_WhenJobIsNotFailedOrDeadLettered(JobStatus status)
    {
        var job = CreateJob(status);
        _repository.Job = job;

        await Assert.ThrowsAsync<InvalidJobTransitionException>(() =>
            new RetryJobCommandHandler(_repository, _publisher).HandleAsync(new RetryJobCommand(job.Id)));

        Assert.False(_repository.Saved);
        Assert.Null(_publisher.Published);
    }

    [Fact]
    public async Task Retry_Throws_WhenJobDoesNotExist()
    {
        _repository.Job = null;

        await Assert.ThrowsAsync<JobNotFoundException>(() =>
            new RetryJobCommandHandler(_repository, _publisher).HandleAsync(new RetryJobCommand(Guid.NewGuid())));
    }

    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Scheduled)]
    public async Task Cancel_MarksQueuedJobCancelled(JobStatus status)
    {
        var job = CreateJob(status);
        _repository.Job = job;

        var result = await new CancelJobCommandHandler(_repository)
            .HandleAsync(new CancelJobCommand(job.Id));

        Assert.Equal(JobStatus.Cancelled, result.Status);
        Assert.NotNull(result.CancelledAt);
        Assert.True(_repository.Saved);
    }

    [Fact]
    public async Task Cancel_MarksProcessingJobCancelled_ForCooperativeStopping()
    {
        var job = CreateJob(JobStatus.Processing);
        job.WorkerId = "worker-1";
        _repository.Job = job;

        var result = await new CancelJobCommandHandler(_repository)
            .HandleAsync(new CancelJobCommand(job.Id));

        // The worker polls this state and cancels the running handler cooperatively.
        Assert.Equal(JobStatus.Cancelled, result.Status);
        Assert.NotNull(result.CancelledAt);
        Assert.True(_repository.Saved);
    }

    [Theory]
    [InlineData(JobStatus.Completed)]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.DeadLettered)]
    [InlineData(JobStatus.Cancelled)]
    public async Task Cancel_Throws_WhenJobIsInTerminalState(JobStatus status)
    {
        var job = CreateJob(status);
        _repository.Job = job;

        await Assert.ThrowsAsync<InvalidJobTransitionException>(() =>
            new CancelJobCommandHandler(_repository).HandleAsync(new CancelJobCommand(job.Id)));

        Assert.False(_repository.Saved);
    }

    [Fact]
    public async Task Cancel_Throws_WhenJobDoesNotExist()
    {
        _repository.Job = null;

        await Assert.ThrowsAsync<JobNotFoundException>(() =>
            new CancelJobCommandHandler(_repository).HandleAsync(new CancelJobCommand(Guid.NewGuid())));
    }

    private static Job CreateJob(JobStatus status)
    {
        var job = Job.Create(JobTypes.SendEmail, "{}", maxAttempts: 3);
        job.Status = status;
        return job;
    }

    private sealed class FakeJobRepository : IJobRepository
    {
        private readonly List<string>? _operations;

        public FakeJobRepository(List<string>? operations = null)
        {
            _operations = operations;
        }

        public Job? Job { get; set; }

        public bool Saved { get; private set; }

        public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Job);

        public Task<Job?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Job);

        public Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.FromResult<Job?>(null);

        public Task<IReadOnlyList<Job>> GetStuckProcessingJobsAsync(
            DateTime heartbeatCutoff,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Job>>([]);

        public Task<IReadOnlyList<Job>> GetDueScheduledJobsAsync(
            DateTime dueBeforeUtc,
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Job>>([]);

        public Task<IReadOnlyList<JobAttempt>> GetAttemptsAsync(Guid jobId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<JobAttempt>>([]);

        public Task<(IReadOnlyList<Job> Items, int TotalCount)> ListAsync(
            JobStatus? status,
            string? type,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<Job> Items, int TotalCount)>(([], 0));

        public Task AddAsync(Job job, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RefreshAsync(Job job, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _operations?.Add("save");
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeJobPublisher : IJobPublisher
    {
        private readonly List<string>? _operations;

        public FakeJobPublisher(List<string>? operations = null)
        {
            _operations = operations;
        }

        public (Guid JobId, string Type)? Published { get; private set; }

        public Task PublishAsync(Guid jobId, string type, CancellationToken cancellationToken = default)
        {
            _operations?.Add("publish");
            Published = (jobId, type);
            return Task.CompletedTask;
        }
    }
}
