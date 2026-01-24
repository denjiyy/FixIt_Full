using AspNetCore.Identity.Mongo.Model;
using MongoDB.Bson.Serialization.Attributes;
using FixIt.Models.Enums;

namespace FixIt.Models.Users;

public class ApplicationUser : MongoUser
{
    public string DisplayName { get; set; } = null!;
    public string? Bio { get; set; }

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string? AvatarMediaId { get; set; }

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string? PreferredCityId { get; set; }

    /// <summary>
    /// The user's role in the system
    /// This determines their permissions
    /// </summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>
    /// For verified officials: their department or agency
    /// </summary>
    public string? OfficialDepartment { get; set; }

    /// <summary>
    /// For verified officials: their title or position
    /// </summary>
    public string? OfficialTitle { get; set; }

    /// <summary>
    /// Whether this is a verified government official account
    /// These users can create OfficialResponse documents
    /// </summary>
    public bool IsVerifiedOfficial { get; set; } = false;

    /// <summary>
    /// Quick denormalized reputation score for display
    /// The full details are in the UserReputation collection
    /// This avoids joins when showing user lists
    /// </summary>
    public int ReputationScore { get; set; } = 0;

    /// <summary>
    /// Quick denormalized trust level for permission checks
    /// </summary>
    public int TrustLevel { get; set; } = 0;

    public bool IsDeleted { get; set; }

    /// <summary>
    /// When the user was soft-deleted (if applicable)
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? DeletedAt { get; set; }
}