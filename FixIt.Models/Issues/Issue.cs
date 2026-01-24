using FixIt.Models.Enums;
using FixIt.Models.Common;
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

    public HashSet<string> Tags { get; set; } = new HashSet<string>();

    // CHANGED: Use GeographicCoordinates for accurate 2dsphere calculations
    public GeoJsonPoint<GeoJson2DGeographicCoordinates> Location { get; set; } = null!;

    public string? Address { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string CityId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string NeighborhoodId { get; set; } = null!;

    // CHANGED: Denormalized Reporter info for fast feed loading
    public UserSummary Reporter { get; set; } = null!;

    public IssueStatus Status { get; set; } = IssueStatus.New;
    
    // CHANGED: Embedded history to track status changes without extra queries
    public List<IssueStatusHistory> StatusHistory { get; set; } = new();

    public IssuePriority Priority { get; set; } = IssuePriority.Medium;

    public int Upvotes { get; set; }
    public int Downvotes { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public HashSet<string> MediaIds { get; set; } = new HashSet<string>();

    public bool IsPinned { get; set; }
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
