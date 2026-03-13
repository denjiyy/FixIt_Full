using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Accessibility;

/// <summary>
/// Tracks translations of issues, comments, and responses
/// Ensures all users can engage regardless of language
/// </summary>
public class TranslationRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Type of content translated: Issue, Comment, OfficialResponse
    /// </summary>
    public string ContentType { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string ContentId { get; set; } = null!;

    /// <summary>
    /// Original language of content
    /// </summary>
    public string SourceLanguage { get; set; } = null!;

    /// <summary>
    /// Language translated to
    /// </summary>
    public string TargetLanguage { get; set; } = null!;

    /// <summary>
    /// Original text before translation
    /// </summary>
    public string OriginalText { get; set; } = null!;

    /// <summary>
    /// Translated text
    /// </summary>
    public string TranslatedText { get; set; } = null!;

    /// <summary>
    /// Translation service used (Google, Microsoft, etc)
    /// </summary>
    public string TranslationService { get; set; } = "Google";

    /// <summary>
    /// Confidence/quality score from translation service
    /// </summary>
    public double? TranslationConfidence { get; set; }

    /// <summary>
    /// Whether human has reviewed/corrected this translation
    /// </summary>
    public bool IsHumanReviewed { get; set; } = false;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? ReviewedByUserId { get; set; }

    /// <summary>
    /// Community can vote if translation is accurate
    /// </summary>
    public int AccuracyVotes { get; set; } = 0;
    public int InnaccuracyVotes { get; set; } = 0;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a supported language and its configuration
/// </summary>
public class SupportedLanguage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>
    /// ISO 639-1 language code (e.g., "en", "bg", "es")
    /// </summary>
    public string LanguageCode { get; set; } = null!;

    /// <summary>
    /// Human-readable language name
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Whether auto-translation is enabled for this language
    /// </summary>
    public bool AutoTranslateEnabled { get; set; } = true;

    /// <summary>
    /// Priority for translation (higher = more important)
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    /// Percentage of city population that speaks this language (for targeting)
    /// </summary>
    public double PopulationPercentage { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
