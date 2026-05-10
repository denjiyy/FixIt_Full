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

    /// <summary>
    /// Local file storage path (legacy, preserved for backward compatibility with existing uploads)
    /// </summary>
    public string? StoragePath { get; set; }

    /// <summary>
    /// Cloudinary secure URL for media hosted on Cloudinary (used for new uploads)
    /// </summary>
    public string? CloudinaryUrl { get; set; }

    public string? ThumbnailPath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

