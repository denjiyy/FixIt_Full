using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using FixIt.Models.Enums;

namespace FixIt.Models.Notifications;

/// <summary>
/// Represents an actual notification sent to a user.
/// This is separate from NotificationSubscription which defines WHAT to watch.
/// This tracks WHAT WAS SENT and whether the user has seen it.
/// </summary>
public class Notification
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    /// <summary>
    /// The type of notification (e.g., "IssueNearby", "CommentOnYourIssue", "StatusChange")
    /// </summary>
    public string Type { get; set; } = null!;

    /// <summary>
    /// User-friendly title for the notification
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Detailed message content
    /// </summary>
    public string Message { get; set; } = null!;

    /// <summary>
    /// The issue this notification is about (if applicable)
    /// </summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public string? RelatedIssueId { get; set; }

    /// <summary>
    /// The comment this notification is about (if applicable)
    /// </summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public string? RelatedCommentId { get; set; }

    /// <summary>
    /// URL the user should be taken to when they click the notification
    /// </summary>
    public string? ActionUrl { get; set; }

    /// <summary>
    /// Has the user read this notification?
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// When the user marked this as read (if they did)
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Channels this notification was sent through
    /// </summary>
    public HashSet<NotificationChannel> Channels { get; set; } = new HashSet<NotificationChannel>();

    /// <summary>
    /// Priority level for notification delivery
    /// </summary>
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Notifications expire after a certain time to avoid clutter
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ExpiresAt { get; set; }
}