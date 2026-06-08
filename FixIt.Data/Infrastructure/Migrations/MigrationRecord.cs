using MongoDB.Bson;

namespace FixIt.Data.Infrastructure.Migrations;

/// <summary>
/// Tracks applied migrations in a special collection
/// Allows resuming migrations if process is interrupted
/// </summary>
public class MigrationRecord
{
    public ObjectId Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public string? Error { get; set; }
    public bool Success { get; set; }
}
