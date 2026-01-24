using FixIt.Models.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Media;

public class Media
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string OwnerId { get; set; } = null!;

    public MediaType Type { get; set; }
    public string MimeType { get; set; } = null!;
    public long SizeBytes { get; set; }

    public string StoragePath { get; set; } = null!;
    public string? ThumbnailPath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
