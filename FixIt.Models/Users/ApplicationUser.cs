using AspNetCore.Identity.Mongo.Model;
using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Users;

public class ApplicationUser : MongoUser
{
    public string DisplayName { get; set; } = null!;
    public string? Bio { get; set; }

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string? AvatarMediaId { get; set; }

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string? PreferredCityId { get; set; }

    public bool IsDeleted { get; set; }
}
