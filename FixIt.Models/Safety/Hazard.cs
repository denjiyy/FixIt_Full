using FixIt.Models.Common;
using FixIt.Models.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace FixIt.Models.Safety;

public enum HazardType
{
    Accident,
    Construction,
    Pothole,
    Flooding,
    DamagedInfrastructure,
    StreetLight,
    Debris,
    TrafficCongestion,
    Crime,
    Other
}

public enum HazardSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public class Hazard
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    public HazardType Type { get; set; }
    public HazardSeverity Severity { get; set; } = HazardSeverity.Medium;

    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;

    public GeoJsonPoint<GeoJson2DGeographicCoordinates> Location { get; set; } = null!;
    public string? Address { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string CityId { get; set; } = null!;

    // Reporter information - null if anonymous
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ReportedByUserId { get; set; }

    public bool IsAnonymous { get; set; } = false;

    // If anonymous, we still track the user for moderation but don't display publicly
    [BsonRepresentation(BsonType.ObjectId)]
    public string? InternalUserId { get; set; }

    public int Confirmations { get; set; } = 0;

    [BsonRepresentation(BsonType.ObjectId)]
    public HashSet<string> ConfirmedByUserIds { get; set; } = new HashSet<string>();

    public bool IsResolved { get; set; } = false;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ResolvedAt { get; set; }

    public string? ResolutionNotes { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? ResolvedByUserId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public HashSet<string> MediaIds { get; set; } = new HashSet<string>();

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ExpiresAt { get; set; }

    public int Version { get; set; } = 1;
}
