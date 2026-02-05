using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Locations;

public class City
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string Country { get; set; } = null!;

    public string? PhotoUrl { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Latitude of city center (for map centering)
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude of city center (for map centering)
    /// </summary>
    public double Longitude { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
