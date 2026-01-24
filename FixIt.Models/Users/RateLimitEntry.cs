using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Users;

/// <summary>
/// Tracks user activity for rate limiting purposes.
/// This prevents abuse by limiting how many actions a user can take in a time window.
/// Uses a sliding window approach for more accurate rate limiting.
/// </summary>
public class RateLimitEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>
    /// The user being tracked
    /// Can also be an IP address for anonymous users
    /// </summary>
    public string Identifier { get; set; } = null!;

    /// <summary>
    /// Type of action being tracked
    /// (e.g., "CreateIssue", "CreateComment", "Vote", "Report")
    /// </summary>
    public string ActionType { get; set; } = null!;

    /// <summary>
    /// Timestamp of when this action occurred
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional metadata about the action
    /// (e.g., IP address, user agent, device info)
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// When this record should expire from the database
    /// MongoDB can automatically delete expired documents using TTL index
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Configuration for rate limits on different actions.
/// This would typically be stored in configuration rather than the database,
/// but I'm including it here for completeness.
/// </summary>
public class RateLimitConfig
{
    public string ActionType { get; set; } = null!;
    
    /// <summary>
    /// Maximum number of actions allowed in the time window
    /// </summary>
    public int MaxActions { get; set; }
    
    /// <summary>
    /// Time window in minutes
    /// </summary>
    public int WindowMinutes { get; set; }
    
    /// <summary>
    /// Different limits for different trust levels
    /// Higher trust = higher limits
    /// </summary>
    public Dictionary<int, int>? TrustLevelOverrides { get; set; }
}

/// <summary>
/// Example rate limit configurations:
/// 
/// New users (trust level 0):
/// - Create Issue: 3 per hour
/// - Create Comment: 10 per hour  
/// - Vote: 20 per hour
/// 
/// Trusted users (trust level 2+):
/// - Create Issue: 10 per hour
/// - Create Comment: 30 per hour
/// - Vote: 100 per hour
/// 
/// This prevents spam while not hindering legitimate users.
/// </summary>