using MongoDB.Bson.Serialization.Attributes;
using FixIt.Models.Common;

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

    /// <summary>
    /// Author information including display name and avatar
    /// </summary>
    public UserSummary? Author { get; set; }

    /// <summary>
    /// Whether the comment was posted anonymously
    /// </summary>
    public bool IsAnonymous { get; set; } = false;

    public string Text { get; set; } = null!;

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public HashSet<string> MediaIds { get; set; } = new HashSet<string>();

    public bool IsDeleted { get; set; }

    /// <summary>
    /// User IDs that have liked this comment
    /// </summary>
    public HashSet<string> LikedBy { get; set; } = new HashSet<string>();

    /// <summary>
    /// User IDs that have disliked this comment
    /// </summary>
    public HashSet<string> DislikedBy { get; set; } = new HashSet<string>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
