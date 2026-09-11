using JobQueue.Application.Abstractions;
using JobQueue.Application.Jobs.Services;

namespace JobQueue.UnitTests;

public class JobErrorClassifierTests
{
    private readonly JobErrorClassifier _classifier = new();

    [Theory]
    [InlineData(typeof(TransientJobException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(OperationCanceledException))]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(HttpRequestException))]
    public void IsTransient_ReturnsTrue_ForTransientErrorTypes(Type exceptionType)
    {
        var exception = CreateException(exceptionType);

        Assert.True(_classifier.IsTransient(exception));
    }

    [Theory]
    [InlineData(typeof(PermanentJobException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(ArgumentNullException))]
    [InlineData(typeof(KeyNotFoundException))]
    public void IsTransient_ReturnsFalse_ForPermanentErrorTypes(Type exceptionType)
    {
        var exception = CreateException(exceptionType);

        Assert.False(_classifier.IsTransient(exception));
    }

    [Fact]
    public void IsTransient_DefaultsToTrue_ForUnknownExceptionTypes()
    {
        var exception = new CustomUnknownException();

        Assert.True(_classifier.IsTransient(exception));
    }

    private static Exception CreateException(Type exceptionType)
    {
        if (exceptionType == typeof(ArgumentNullException))
        {
            return new ArgumentNullException("param");
        }

        return (Exception)Activator.CreateInstance(exceptionType, "test failure")!;
    }

    private sealed class CustomUnknownException : Exception
    {
        public CustomUnknownException()
            : base("unknown failure")
        {
        }
    }
}
