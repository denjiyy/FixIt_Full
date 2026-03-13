using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Transparency;

/// <summary>
/// Before/After photos and evidence showing issue resolution
/// Provides visual proof of work completed and satisfying user experience
/// </summary>
public class IssueResolutionEvidence
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string IssueId { get; set; } = null!;

    /// <summary>
    /// Before photo IDs (from when issue was first reported)
    /// </summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> BeforePhotoIds { get; set; } = new();

    /// <summary>
    /// After photo IDs (taken after resolution)
    /// </summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> AfterPhotoIds { get; set; } = new();

    /// <summary>
    /// Who submitted the after photos (can be municipality or original reporter)
    /// </summary>
    [BsonRepresentation(BsonType.ObjectId)]
    public string SubmittedByUserId { get; set; } = null!;

    /// <summary>
    /// Detailed description of work completed
    /// </summary>
    public string ResolutionDescription { get; set; } = null!;

    /// <summary>
    /// Contractor or department who completed the work
    /// </summary>
    public string? CompletedByEntityName { get; set; }

    /// <summary>
    /// Date work was completed
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ResolutionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Cost of repair/improvement (if available)
    /// </summary>
    public decimal? EstimatedCost { get; set; }

    /// <summary>
    /// Public engagement score (likes/positive feedback)
    /// </summary>
    public int PositiveFeedbackCount { get; set; } = 0;

    /// <summary>
    /// Whether this was featured as success story
    /// </summary>
    public bool IsFeatured { get; set; } = false;

    /// <summary>
    /// Shareable before/after comparison is enabled
    /// </summary>
    public bool AllowSocialMediaShare { get; set; } = true;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
