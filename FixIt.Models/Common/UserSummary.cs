using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FixIt.Models.Common;

public class UserSummary
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? AvatarUrl { get; set; }
}