using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.AI;

/// <summary>
/// Type of admin suggestion
/// </summary>
public enum SuggestionType
{
    ReportAction,           // Recommend approve/reject/dismiss a report
    IssuePriority,          // Suggest priority adjustment
    IssueDuplicateWarning,  // Warn about potential duplicates
    IssueResolution,        // Suggest marking as resolved
    UserModeration,         // Suggest user restrictions/bans
    ResourceAllocation      // Suggest assigning to team members
}

/// <summary>
/// Confidence level of the suggestion
/// </summary>
public enum ConfidenceLevel
{
    Low,
    Medium,
    High,
    VeryHigh
}

/// <summary>
/// AI-generated suggestion for admin/moderator actions
/// </summary>
public class AdminSuggestion
{
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

    /// <summary>
    /// Type of suggestion
    /// </summary>
    public SuggestionType Type { get; set; }

    /// <summary>
    /// What entity this suggestion applies to
    /// </summary>
    public string TargetEntityId { get; set; } = null!;

    /// <summary>
    /// Type of target (Report, Issue, User, etc)
    /// </summary>
    public string TargetEntityType { get; set; } = null!;

    /// <summary>
    /// Main suggestion text
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Detailed explanation
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Confidence in this suggestion (0-100)
    /// </summary>
    public int ConfidenceScore { get; set; }

    /// <summary>
    /// Confidence level category
    /// </summary>
    public ConfidenceLevel ConfidenceLevel { get; set; }

    /// <summary>
    /// Recommended action (e.g., "Uphold", "Dismiss", "Mark as Resolved")
    /// </summary>
    public string RecommendedAction { get; set; } = null!;

    /// <summary>
    /// Reasoning for the suggestion
    /// </summary>
    public string Reasoning { get; set; } = null!;

    /// <summary>
    /// Supporting data (keywords, related issues, patterns found)
    /// </summary>
    public List<string> SupportingData { get; set; } = new();

    /// <summary>
    /// Related entity IDs (for duplicates, related reports, etc)
    /// </summary>
    public List<string> RelatedEntityIds { get; set; } = new();

    /// <summary>
    /// Whether this suggestion has been acted upon
    /// </summary>
    public bool IsActedUpon { get; set; }

    /// <summary>
    /// What action was taken if acted upon
    /// </summary>
    public string? ActionTaken { get; set; }

    /// <summary>
    /// Who acted on this suggestion
    /// </summary>
    public string? ActedByUserId { get; set; }

    /// <summary>
    /// When the suggestion was acted upon
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ActedAt { get; set; }

    /// <summary>
    /// When the suggestion was generated
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether suggestion is still valid/relevant
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Admin who manually invalidated this suggestion
    /// </summary>
    public string? InvalidatedByUserId { get; set; }

    /// <summary>
    /// When suggestion was invalidated
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? InvalidatedAt { get; set; }
}
