using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using FixIt.Models.Enums;

namespace FixIt.Models.Moderation;

public class ContentReport
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string ReporterId { get; set; } = null!;

    public ModerationTargetType TargetType { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string TargetId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? TargetAuthorId { get; set; }

    public ReportReason Reason { get; set; }

    public string? Details { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Pending;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? ReviewedByModeratorId { get; set; }

    public string? ReviewDecision { get; set; }

    public string? ReviewNotes { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ReviewedAt { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? ResultingModerationActionId { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}