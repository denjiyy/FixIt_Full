using FixIt.Models.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Moderation;

public class ModerationAction
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    public ModerationTargetType TargetType { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string TargetId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string ModeratorId { get; set; } = null!;

    public string Action { get; set; } = null!;
    public string Reason { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
