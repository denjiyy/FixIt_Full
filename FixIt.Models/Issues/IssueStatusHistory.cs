using FixIt.Models.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Issues;

public class IssueStatusHistory
{
    public IssueStatus From { get; set; }
    public IssueStatus To { get; set; }

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string ChangedByUserId { get; set; } = null!;

    public string? Comment { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
