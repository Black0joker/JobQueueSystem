namespace JobQueue.Api.Middleware;

/// <summary>
/// Phase 23: ensures every HTTP request carries a correlation id that can be traced
/// across the whole pipeline (HTTP request → job row → RabbitMQ message → consumer →
/// handler). Clients supply their own id through the X-Correlation-Id header; a new
/// one is generated otherwise. The resolved id is stored in <see cref="HttpContext.Items"/>,
/// echoed back in the response header, and added to a logger scope so every
/// request-scoped log entry is enriched with it.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    /// <summary>Header carrying the correlation id in requests and responses.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Key under which the resolved correlation id is stored in <see cref="HttpContext.Items"/>.</summary>
    public const string HttpContextItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Guid.TryParse(context.Request.Headers[HeaderName].FirstOrDefault(), out var parsed)
            ? parsed
            : Guid.NewGuid();

        context.Items[HttpContextItemKey] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId.ToString();
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = correlationId }))
        {
            await _next(context);
        }
    }
}
