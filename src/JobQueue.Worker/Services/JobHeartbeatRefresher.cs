using JobQueue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobQueue.Worker.Services;

/// <summary>
/// Refreshes <c>LastHeartbeatAt</c> for a job at a fixed interval while the worker is
/// processing it (phase 14). It runs in its own DI scope with its own DbContext and
/// raw SQL update, so it never interferes with the consumer's tracked entity. Dispose
/// stops the loop and awaits its exit.
/// </summary>
public sealed class JobHeartbeatRefresher : IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Guid _jobId;
    private readonly TimeSpan _interval;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stop;
    private readonly Task _loop;

    public JobHeartbeatRefresher(
        IServiceScopeFactory scopeFactory,
        Guid jobId,
        TimeSpan interval,
        ILogger logger,
        CancellationToken linkedToken)
    {
        _scopeFactory = scopeFactory;
        _jobId = jobId;
        _interval = interval;
        _logger = logger;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(linkedToken);
        _loop = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(_interval);
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<JobQueueDbContext>();
                    await dbContext.Database.ExecuteSqlAsync(
                        $"UPDATE Jobs SET LastHeartbeatAt = SYSUTCDATETIME() WHERE Id = {_jobId}",
                        _stop.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // A missed heartbeat is not fatal; the next tick retries.
                    _logger.LogWarning(ex, "Failed to refresh the heartbeat for job {JobId}.", _jobId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
    }

    private bool _stopped;

    /// <summary>
    /// Stops the refresh loop and awaits its exit. Call this before saving the job's
    /// final state: every tick changes the row's RowVersion, so the consumer must
    /// resynchronize its tracked entity once the ticks stop. Idempotent.
    /// </summary>
    public async Task StopAsync()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        await _stop.CancelAsync();

        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _stop.Dispose();
    }
}
