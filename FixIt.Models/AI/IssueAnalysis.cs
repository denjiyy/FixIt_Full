using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.AI;

/// <summary>
/// Issue category determined by AI
/// </summary>
public enum IssueCategory
{
    Infrastructure,       // Roads, bridges, utilities
    PublicSafety,        // Crime, accidents, hazards
    EnvironmentalHealth, // Pollution, waste, water
    Parks,               // Parks, recreation, green space
    Transportation,      // Traffic, transit, parking
    Utilities,           // Water, gas, electricity
    Sanitation,          // Trash, cleaning, maintenance
    PublicHealth,        // Disease, health hazards
    Other
}

/// <summary>
/// AI-powered analysis results for an issue
/// </summary>
public class IssueAnalysis
{
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string IssueId { get; set; } = null!;

    /// <summary>
    /// AI-determined category
    /// </summary>
    public IssueCategory Category { get; set; }

    /// <summary>
    /// Confidence score (0-100)
    /// </summary>
    public int ConfidenceScore { get; set; }

    /// <summary>
    /// Why this category was chosen
    /// </summary>
    public string Reasoning { get; set; } = null!;

    /// <summary>
    /// Estimated severity (1-10)
    /// </summary>
    public int EstimatedSeverity { get; set; }

    /// <summary>
    /// Keywords extracted from issue
    /// </summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>
    /// Suggested tags
    /// </summary>
    public List<string> SuggestedTags { get; set; } = new();

    /// <summary>
    /// Potential duplicate issues
    /// </summary>
    public List<DuplicateMatch> PotentialDuplicates { get; set; } = new();

    /// <summary>
    /// When analysis was performed
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Potential duplicate match
/// </summary>
public class DuplicateMatch
{
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string IssueId { get; set; } = null!;

    public string IssueTitle { get; set; } = null!;

    /// <summary>
    /// Similarity score (0-100)
    /// </summary>
    public int SimilarityScore { get; set; }

    public string Reason { get; set; } = null!;
}
