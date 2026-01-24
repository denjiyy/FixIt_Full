using FixIt.Models.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Engagement;

public class Vote
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string IssueId { get; set; } = null!;

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    public VoteType Value { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
