using FixIt.Models.Enums;
using FixIt.Models.Common;
using FixIt.Models.AI;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace FixIt.Models.Issues;

public class Issue
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public HashSet<string> TagIds { get; set; } = new HashSet<string>();

    public GeoJsonPoint<GeoJson2DGeographicCoordinates> Location { get; set; } = null!;

    public string? Address { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string CityId { get; set; } = null!;

    public UserSummary Reporter { get; set; } = null!;

    /// <summary>
    /// Whether the issue was reported anonymously
    /// When true, the reporter's name is not displayed publicly
    /// </summary>
    public bool IsAnonymous { get; set; } = false;

    public IssueStatus Status { get; set; } = IssueStatus.New;
    
    public List<IssueStatusHistory> StatusHistory { get; set; } = new();

    public IssuePriority Priority { get; set; } = IssuePriority.Medium;

    public IssueCategory? Category { get; set; }

    public string? Department { get; set; }

    public int Upvotes { get; set; }
    public int Downvotes { get; set; }

    public int Version { get; set; } = 1;

    [BsonRepresentation(BsonType.ObjectId)]
    public HashSet<string> MediaIds { get; set; } = new HashSet<string>();

    public int CommentCount { get; set; } = 0;

    public int ViewCount { get; set; } = 0;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    public bool IsPinned { get; set; }
    public bool IsDeleted { get; set; }

    public bool IsLocked { get; set; } = false;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
