using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using FixIt.Models.Enums;

namespace FixIt.Models.Issues;

public class OfficialResponse
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string IssueId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string ResponderId { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string ResponderTitle { get; set; } = null!;
    public string Message { get; set; } = null!;
    public IssueStatus? NewStatus { get; set; }
    public string? EstimatedResolution { get; set; }
    public string? ReferenceNumber { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public HashSet<string> AttachmentIds { get; set; } = new HashSet<string>();
    public bool IsPinned { get; set; }
    public bool IsPublic { get; set; } = true;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}