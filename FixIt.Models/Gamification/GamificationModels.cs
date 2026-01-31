using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Gamification;

/// <summary>
/// Represents an achievement badge earned by a user
/// </summary>
public enum AchievementType
{
    FirstReporter,           // Report first issue
    HelpfulCommenteer,       // 5+ helpful comments
    CommunityHelper,         // 10+ helpful comments
    IssueSolver,             // First issue resolved via citizen report
    CivicContributor,        // 50 total reputation points
    CommunityChampion,       // 150 total reputation points
    CivicLeader,             // 300 total reputation points
    AccuratReporter,         // 10 issues accepted/marked useful
    VerifiedCitizen,         // Email verified
    ConsistentParticipant,   // Active for 4+ weeks
    ImpactMaker,             // Reported issue that led to municipal action
    TopContributor,          // In top 10 leaderboard this month
}

/// <summary>
/// Represents an achievement badge with metadata
/// </summary>
public class Achievement
{
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

    public AchievementType Type { get; set; }
    
    public string Name { get; set; } = null!;
    
    public string Description { get; set; } = null!;
    
    public string? Icon { get; set; }
    
    public int PointsReward { get; set; } = 0;
    
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a single reputation transaction/event
/// </summary>
public class ReputationTransaction
{
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

    public string UserId { get; set; } = null!;
    
    public int Points { get; set; }
    
    public string Reason { get; set; } = null!;  // "issue_reported", "comment_upvoted", "issue_resolved", etc
    
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string? RelatedIssueId { get; set; }
    
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string? RelatedCommentId { get; set; }
    
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a user's reputation and achievement status
/// </summary>
public class UserReputation
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = null!;  // Same as UserId

    public string UserId { get; set; } = null!;

    /// <summary>
    /// Total reputation points accumulated
    /// </summary>
    public int TotalPoints { get; set; } = 0;

    /// <summary>
    /// User's trust/experience level (0-3)
    /// 0: New (0-10 pts)
    /// 1: Active (11-50 pts)
    /// 2: Trusted (51-150 pts)
    /// 3: Leader (150+ pts)
    /// </summary>
    public int TrustLevel { get; set; } = 0;

    /// <summary>
    /// Count of issues reported by this user
    /// </summary>
    public int IssuesReported { get; set; } = 0;

    /// <summary>
    /// Count of comments made by this user
    /// </summary>
    public int CommentsPosted { get; set; } = 0;

    /// <summary>
    /// Count of upvotes received on user's content
    /// </summary>
    public int UpvotesReceived { get; set; } = 0;

    /// <summary>
    /// Count of issues that were resolved due to this user's report
    /// </summary>
    public int IssuesResolved { get; set; } = 0;

    /// <summary>
    /// Earned achievement badges
    /// </summary>
    public List<Achievement> Achievements { get; set; } = new();

    /// <summary>
    /// When this reputation record was created
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When reputation was last updated
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a leaderboard entry for ranking users
/// </summary>
public class LeaderboardEntry
{
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

    public string UserId { get; set; } = null!;

    public string UserDisplayName { get; set; } = null!;

    public string? UserAvatarId { get; set; }

    public int Points { get; set; }

    public int Rank { get; set; }

    public int TrustLevel { get; set; }

    public LeaderboardPeriod Period { get; set; }  // "weekly", "monthly", "all_time"

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum LeaderboardPeriod
{
    Weekly,
    Monthly,
    AllTime
}
