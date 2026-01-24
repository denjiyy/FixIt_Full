using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace FixIt.Models.Locations;

public class Neighborhood
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string CityId { get; set; } = null!;

    public string Name { get; set; } = null!;

    public GeoJsonPolygon<GeoJson2DGeographicCoordinates>? Boundary { get; set; }
}
