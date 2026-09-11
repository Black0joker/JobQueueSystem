using System.Text.Json;
using JobQueue.Application.Abstractions;
using JobQueue.Application.Jobs.Services;
using JobQueue.Domain.Jobs;

namespace JobQueue.UnitTests;

public class JobHandlerResolverTests
{
    [Fact]
    public void Resolve_ReturnsHandlerRegisteredForJobType()
    {
        var handler = new FakeJobHandler(JobTypes.SendEmail);
        var resolver = new JobHandlerResolver(new[] { handler });

        var resolved = resolver.Resolve(JobTypes.SendEmail);

        Assert.Same(handler, resolved);
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        var handler = new FakeJobHandler(JobTypes.SendEmail);
        var resolver = new JobHandlerResolver(new[] { handler });

        Assert.Same(handler, resolver.Resolve("sendemail"));
        Assert.Same(handler, resolver.Resolve("SENDEMAIL"));
    }

    [Theory]
    [InlineData("UnknownType")]
    [InlineData("")]
    [InlineData(null)]
    public void Resolve_ReturnsNull_WhenNoHandlerIsRegistered(string? jobType)
    {
        var resolver = new JobHandlerResolver(new[] { new FakeJobHandler(JobTypes.SendEmail) });

        Assert.Null(resolver.Resolve(jobType!));
    }

    [Fact]
    public void Constructor_Throws_WhenTwoHandlersShareTheSameJobType()
    {
        var handlers = new IJobHandler[]
        {
            new FakeJobHandler(JobTypes.SendEmail),
            new FakeJobHandler(JobTypes.SendEmail)
        };

        var exception = Assert.Throws<InvalidOperationException>(() => new JobHandlerResolver(handlers));

        Assert.Contains(JobTypes.SendEmail, exception.Message);
    }

    [Fact]
    public void Resolve_SupportsMultipleDistinctJobTypes()
    {
        var emailHandler = new FakeJobHandler(JobTypes.SendEmail);
        var invoiceHandler = new FakeJobHandler(JobTypes.GenerateInvoice);
        var resolver = new JobHandlerResolver(new IJobHandler[] { emailHandler, invoiceHandler });

        Assert.Same(emailHandler, resolver.Resolve(JobTypes.SendEmail));
        Assert.Same(invoiceHandler, resolver.Resolve(JobTypes.GenerateInvoice));
    }

    private sealed class FakeJobHandler(string jobType) : IJobHandler
    {
        public string JobType { get; } = jobType;

        public Task HandleAsync(JobExecutionContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
