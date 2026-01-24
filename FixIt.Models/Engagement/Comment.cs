using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Engagement;

public class Comment
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string IssueId { get; set; } = null!;

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string AuthorId { get; set; } = null!;

    public string Text { get; set; } = null!;

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public HashSet<string> MediaIds { get; set; } = new HashSet<string>();

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
