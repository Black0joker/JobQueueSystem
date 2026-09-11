namespace JobQueue.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ connection settings bound from the "RabbitMq" configuration section.
/// </summary>
public sealed class RabbitMqOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RabbitMq";

    /// <summary>RabbitMQ host name.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>User name used to connect.</summary>
    public string Username { get; set; } = "guest";

    /// <summary>Password used to connect.</summary>
    public string Password { get; set; } = "guest";
}
