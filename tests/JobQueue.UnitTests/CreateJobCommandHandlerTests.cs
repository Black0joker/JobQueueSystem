using JobQueue.Application.Abstractions;
using JobQueue.Application.Jobs.Commands;
using JobQueue.Domain.Enums;
using JobQueue.Domain.Exceptions;
using JobQueue.Domain.Jobs;

namespace JobQueue.UnitTests;

/// <summary>
/// Phase 18: idempotent job creation rules.
/// </summary>
public class CreateJobCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_CreatesAndPublishesPendingJob()
    {
        var repository = new FakeJobRepository();
        var publisher = new FakeJobPublisher();

        var result = await new CreateJobCommandHandler(repository, publisher).HandleAsync(
            new CreateJobCommand("SendEmail", "{}", 0, 3, null, null, null));

        Assert.False(result.AlreadyExisted);
        Assert.Equal(JobStatus.Pending, result.Job.Status);
        Assert.Equal(result.Job.Id, publisher.Published!.Value.JobId);
    }

    [Fact]
    public async Task HandleAsync_BuffersPublishBeforeSave_ForTransactionalOutbox()
    {
        // Phase 22: the MassTransit EF outbox persists the published message inside the
        // SaveChanges transaction, so the publish must be buffered before the save for
        // the job row and the outbox message to commit atomically.
        var operations = new List<string>();
        var repository = new FakeJobRepository(operations);
        var publisher = new FakeJobPublisher(operations);

        await new CreateJobCommandHandler(repository, publisher).HandleAsync(
            new CreateJobCommand("SendEmail", "{}", 0, 3, null, null, null));

        var publishIndex = operations.IndexOf("publish");
        var saveIndex = operations.IndexOf("save");

        Assert.NotEqual(-1, publishIndex);
        Assert.NotEqual(-1, saveIndex);
        Assert.True(publishIndex < saveIndex);
    }

    [Fact]
    public async Task HandleAsync_DoesNotPublishScheduledJob()
    {
        var repository = new FakeJobRepository();
        var publisher = new FakeJobPublisher();

        var result = await new CreateJobCommandHandler(repository, publisher).HandleAsync(
            new CreateJobCommand("SendEmail", "{}", 0, 3, DateTime.UtcNow.AddHours(1), null, null));

        Assert.Equal(JobStatus.Scheduled, result.Job.Status);
        Assert.Null(publisher.Published);
    }

    [Fact]
    public async Task HandleAsync_ReturnsExistingJob_WhenIdempotencyKeyAlreadyUsed()
    {
        var existing = Job.Create("SendEmail", "{}", idempotencyKey: "key-1");
        var repository = new FakeJobRepository { IdempotencyLookup = existing };
        var publisher = new FakeJobPublisher();

        var result = await new CreateJobCommandHandler(repository, publisher).HandleAsync(
            new CreateJobCommand("SendEmail", "{}", 0, 3, null, "key-1", null));

        Assert.True(result.AlreadyExisted);
        Assert.Equal(existing.Id, result.Job.Id);
        Assert.Null(publisher.Published);
        Assert.False(repository.Added);
    }

    [Fact]
    public async Task HandleAsync_ReturnsExistingJob_WhenConcurrentInsertWinsTheRace()
    {
        var existing = Job.Create("SendEmail", "{}", idempotencyKey: "key-1");
        var repository = new FakeJobRepository
        {
            IdempotencyLookupAfterRace = existing,
            ThrowDuplicateOnAdd = true
        };
        var publisher = new FakeJobPublisher();

        var result = await new CreateJobCommandHandler(repository, publisher).HandleAsync(
            new CreateJobCommand("SendEmail", "{}", 0, 3, null, "key-1", null));

        Assert.True(result.AlreadyExisted);
        Assert.Equal(existing.Id, result.Job.Id);
        Assert.Null(publisher.Published);
    }

    private sealed class FakeJobRepository : IJobRepository
    {
        private readonly List<string>? _operations;

        public FakeJobRepository(List<string>? operations = null)
        {
            _operations = operations;
        }

        public Job? IdempotencyLookup { get; init; }

        public Job? IdempotencyLookupAfterRace { get; init; }

        public bool ThrowDuplicateOnAdd { get; init; }

        public bool Added { get; private set; }

        private bool _firstLookup = true;

        public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Job?>(null);

        public Task<Job?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Job?>(null);

        public Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        {
            var result = _firstLookup ? IdempotencyLookup : IdempotencyLookupAfterRace;
            _firstLookup = false;
            return Task.FromResult(result);
        }

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
        {
            if (ThrowDuplicateOnAdd)
            {
                throw new DuplicateJobException("duplicate");
            }

            Added = true;
            return Task.CompletedTask;
        }

        public Task RefreshAsync(Job job, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _operations?.Add("save");
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
