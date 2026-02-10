using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Issues;

/// <summary>
/// Tracks when a user views an issue to prevent duplicate view counts
/// </summary>
public class ViewEvent
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string IssueId { get; set; } = null!;

    /// <summary>
    /// User ID of the viewer. Can be null for anonymous users.
    /// </summary>
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string? UserId { get; set; }

    /// <summary>
    /// Session ID for tracking anonymous users
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// IP address for additional tracking
    /// </summary>
    public string? IpAddress { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
}
