using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Issues;

/// <summary>
/// Tracks changes to issue fields (priority, status, location, etc.)
/// for audit logging and change history
/// </summary>
public class IssueFieldHistory
{
    /// <summary>
    /// The name of the field that was changed (e.g., "Priority", "Status", "Location", "Description")
    /// </summary>
    public string FieldName { get; set; } = null!;

    /// <summary>
    /// The previous value (can be null for newly added fields)
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// The new value
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// User ID of who made the change
    /// </summary>
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string ChangedByUserId { get; set; } = null!;

    /// <summary>
    /// Optional comment explaining why the change was made
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Timestamp of when the change was made
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
