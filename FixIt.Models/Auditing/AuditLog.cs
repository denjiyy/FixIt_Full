using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Auditing;

public class AuditLog
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    public string Action { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string ActorId { get; set; } = null!;

    public string Target { get; set; } = null!;

    public object? Payload { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
