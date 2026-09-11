using JobQueue.Domain.Enums;
using JobQueue.Domain.Exceptions;
using JobQueue.Domain.Jobs;

namespace JobQueue.UnitTests;

/// <summary>
/// Phase 20: centralized job state machine rules.
/// </summary>
public class JobStatusTransitionTests
{
    [Theory]
    [InlineData(JobStatus.Scheduled, JobStatus.Pending)]       // dispatcher
    [InlineData(JobStatus.Scheduled, JobStatus.Cancelled)]     // cancel before due
    [InlineData(JobStatus.Pending, JobStatus.Processing)]      // worker claim
    [InlineData(JobStatus.Pending, JobStatus.Cancelled)]       // cancel while queued
    [InlineData(JobStatus.Processing, JobStatus.Completed)]    // success
    [InlineData(JobStatus.Processing, JobStatus.Failed)]       // permanent error
    [InlineData(JobStatus.Processing, JobStatus.DeadLettered)] // retries exhausted
    [InlineData(JobStatus.Processing, JobStatus.Cancelled)]    // operator cancellation
    [InlineData(JobStatus.Processing, JobStatus.Pending)]      // stuck-job recovery reset
    [InlineData(JobStatus.Failed, JobStatus.Pending)]          // manual retry
    [InlineData(JobStatus.DeadLettered, JobStatus.Pending)]    // manual retry
    public void CanTransition_AllowsEveryLegalTransition(JobStatus from, JobStatus to)
    {
        Assert.True(JobStatusTransitions.CanTransition(from, to));
    }

    [Theory]
    [InlineData(JobStatus.Pending, JobStatus.Completed)]
    [InlineData(JobStatus.Pending, JobStatus.Failed)]
    [InlineData(JobStatus.Pending, JobStatus.DeadLettered)]
    [InlineData(JobStatus.Pending, JobStatus.Scheduled)]
    [InlineData(JobStatus.Scheduled, JobStatus.Processing)]
    [InlineData(JobStatus.Scheduled, JobStatus.Completed)]
    [InlineData(JobStatus.Processing, JobStatus.Scheduled)]
    [InlineData(JobStatus.Completed, JobStatus.Pending)]
    [InlineData(JobStatus.Completed, JobStatus.Processing)]
    [InlineData(JobStatus.Cancelled, JobStatus.Pending)]
    [InlineData(JobStatus.Cancelled, JobStatus.Processing)]
    public void CanTransition_RejectsIllegalTransitions(JobStatus from, JobStatus to)
    {
        Assert.False(JobStatusTransitions.CanTransition(from, to));
    }

    [Fact]
    public void TransitionTo_UpdatesStatus_WhenTransitionIsValid()
    {
        var job = Job.Create("SendEmail", "{}");

        Assert.Equal(JobStatus.Pending, job.Status);

        job.TransitionTo(JobStatus.Processing);
        Assert.Equal(JobStatus.Processing, job.Status);

        job.TransitionTo(JobStatus.Completed);
        Assert.Equal(JobStatus.Completed, job.Status);
    }

    [Fact]
    public void TransitionTo_Throws_WhenTransitionIsInvalid()
    {
        var job = Job.Create("SendEmail", "{}");
        job.TransitionTo(JobStatus.Processing);
        job.TransitionTo(JobStatus.Completed);

        var exception = Assert.Throws<InvalidJobTransitionException>(
            () => job.TransitionTo(JobStatus.Pending));

        Assert.Contains("'Completed'", exception.Message);
        Assert.Contains("'Pending'", exception.Message);
        Assert.Equal(JobStatus.Completed, job.Status);
    }

    [Fact]
    public void TransitionTo_KeepsStatus_WhenThrows()
    {
        var job = Job.Create("SendEmail", "{}", scheduledAt: DateTime.UtcNow.AddHours(1));

        Assert.Equal(JobStatus.Scheduled, job.Status);
        Assert.Throws<InvalidJobTransitionException>(() => job.TransitionTo(JobStatus.Processing));
        Assert.Equal(JobStatus.Scheduled, job.Status);
    }
}
