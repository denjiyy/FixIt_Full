using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Data.Infrastructure.Migrations;

/// <summary>
/// Mongo-backed advisory lock for the migration runner. Coordinates concurrent
/// startups (rolling deploys) so only one instance executes a given pending
/// migration; the rest wait for the lock to release.
/// </summary>
public class MigrationLock
{
    public ObjectId Id { get; set; }

    /// <summary>Constant well-known name (currently always "global").</summary>
    public string Name { get; set; } = "global";

    /// <summary>
    /// Identifier of the instance that holds the lock. Null when free.
    /// Format: machine:processId:guid — informative for forensics, not parsed.
    /// </summary>
    public string? Owner { get; set; }

    /// <summary>
    /// When the current Owner acquired the lock. Locks older than
    /// MigrationRunner.LockTtl are considered abandoned and can be stolen.
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime AcquiredAt { get; set; }
}
