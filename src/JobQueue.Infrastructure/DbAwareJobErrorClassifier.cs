using JobQueue.Application.Abstractions;
using JobQueue.Application.Jobs.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace JobQueue.Infrastructure;

/// <summary>
/// Extends the application-level classification with database-specific rules:
/// EF Core update failures and SQL Server errors (connection drops, deadlocks,
/// timeouts) are transient. Every other exception falls back to the base rules.
/// </summary>
public sealed class DbAwareJobErrorClassifier : JobErrorClassifier
{
    public override bool IsTransient(Exception exception)
        => exception switch
        {
            DbUpdateException => true,
            SqlException => true,
            _ => base.IsTransient(exception)
        };
}
