using JobQueue.Application.Abstractions;
using JobQueue.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobQueue.IntegrationTests;

public class DbAwareJobErrorClassifierTests
{
    private readonly DbAwareJobErrorClassifier _classifier = new();

    [Fact]
    public void IsTransient_ReturnsTrue_ForEfCoreUpdateFailures()
    {
        Assert.True(_classifier.IsTransient(new DbUpdateException("concurrent update")));
        Assert.True(_classifier.IsTransient(new DbUpdateConcurrencyException("row version mismatch")));
    }

    [Fact]
    public void IsTransient_FallsBackToApplicationRules_ForNonDatabaseExceptions()
    {
        Assert.True(_classifier.IsTransient(new TimeoutException("timeout")));
        Assert.False(_classifier.IsTransient(new PermanentJobException("invalid payload")));
        Assert.True(_classifier.IsTransient(new InvalidOperationException("unknown problem")));
    }
}
