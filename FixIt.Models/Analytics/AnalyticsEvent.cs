using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Analytics;

public class AnalyticsEvent
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    public string EventType { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? UserId { get; set; }

    public object? Properties { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
