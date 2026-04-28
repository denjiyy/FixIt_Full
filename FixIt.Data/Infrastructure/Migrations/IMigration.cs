using MongoDB.Driver;

namespace FixIt.Data.Infrastructure.Migrations;

/// <summary>
/// Base interface for database migrations
/// Migrations are run in version order (001, 002, 003, etc.)
/// </summary>
public interface IMigration
{
    /// <summary>
    /// Migration version number (format: YYYYMMDD_001, YYYYMMDD_002, etc.)
    /// Must be unique and sortable in ascending order
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Human-readable description of the migration
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Execute the migration
    /// Should handle idempotency - safe to run multiple times
    /// </summary>
    Task UpAsync(IMongoDatabase database);

    /// <summary>
    /// Rollback the migration (optional)
    /// Return false if rollback is not supported
    /// </summary>
    Task<bool> DownAsync(IMongoDatabase database) => Task.FromResult(false);
}
