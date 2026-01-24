using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using FixIt.Models.Enums;

namespace FixIt.Models.Media;

/// <summary>
/// Tracks where media is being used across the application.
/// This solves the orphaned media problem and enables safe deletion.
/// 
/// When you upload a photo, it might be attached to an issue initially,
/// then later referenced in a comment, then used in an official response.
/// Without tracking these references, you can't safely delete the photo
/// without breaking links elsewhere in the system.
/// </summary>
public class MediaReference
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>
    /// The media file being referenced
    /// </summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public string MediaId { get; set; } = null!;

    /// <summary>
    /// Type of entity that's using this media
    /// </summary>
    public MediaReferenceType ReferenceType { get; set; }

    /// <summary>
    /// The ID of the entity using this media
    /// </summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public string ReferenceId { get; set; } = null!;

    /// <summary>
    /// When this reference was created
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Example usage:
/// 
/// When a user uploads photos to an issue:
/// 1. Upload photos to storage, create Media documents
/// 2. Create MediaReference documents linking each photo to the issue
/// 3. Add photo IDs to the Issue.MediaIds set
/// 
/// When deleting an issue:
/// 1. Find all MediaReferences where ReferenceType=Issue and ReferenceId=issueId
/// 2. For each media, check if there are OTHER references to it
/// 3. If a media file has NO other references, it's safe to delete from storage
/// 4. Delete the MediaReference documents
/// 
/// This prevents:
/// - Orphaned files wasting storage
/// - Accidentally deleting files still in use elsewhere
/// - Broken image links
/// </summary>