namespace JobQueue.Domain.Jobs;

/// <summary>
/// Well-known job type identifiers used by the API and matched by job handlers.
/// </summary>
public static class JobTypes
{
    /// <summary>Sends an email (simulated).</summary>
    public const string SendEmail = nameof(SendEmail);

    /// <summary>Generates an invoice document (simulated CPU/file work).</summary>
    public const string GenerateInvoice = nameof(GenerateInvoice);

    /// <summary>Updates inventory records (simulated database work).</summary>
    public const string UpdateInventory = nameof(UpdateInventory);

    /// <summary>Notifies a user through an external service (simulated HTTP call).</summary>
    public const string NotifyUser = nameof(NotifyUser);
}
