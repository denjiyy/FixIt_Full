using AspNetCore.Identity.Mongo.Model;
using MongoDB.Bson.Serialization.Attributes;
using FixIt.Models.Enums;
using System.Collections.Generic;

namespace FixIt.Models.Users;

public class ExternalIdentity
{
    public string Provider { get; set; } = null!; // "Google", "GitHub", "Microsoft"
    public string ProviderId { get; set; } = null!; // External provider's user ID
    public string? ProviderUsername { get; set; }
    public string? ProviderDisplayName { get; set; }
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LastSignInAt { get; set; }
}

public class ApplicationUser : MongoUser
{
    public string DisplayName { get; set; } = null!;
    public string? Bio { get; set; }

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string? AvatarMediaId { get; set; }

    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string? PreferredCityId { get; set; }

    /// <summary>
    /// External identity providers linked to this account
    /// Enables users to sign in via Google, GitHub, Microsoft, etc.
    /// </summary>
    public List<ExternalIdentity> ExternalIdentities { get; set; } = new();

    /// <summary>
    /// Whether the user created account via password or OAuth
    /// </summary>
    public bool HasPasswordAuth { get; set; } = true;

    /// <summary>
    /// Whether user can post anonymously
    /// </summary>
    public bool AnonymousReportingEnabled { get; set; } = true;

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

    /// <summary>
    /// Whether user has enabled email notifications
    /// </summary>
    public bool EmailNotificationsEnabled { get; set; } = true;

    /// <summary>
    /// Whether user wants to receive health reports
    /// </summary>
    public bool ReceiveHealthReports { get; set; } = true;

    /// <summary>
    /// Whether user wants to receive hazard alerts
    /// </summary>
    public bool ReceiveHazardAlerts { get; set; } = true;

    /// <summary>
    /// Whether user wants to receive weekly reminders
    /// </summary>
    public bool ReceiveWeeklyReminders { get; set; } = true;

    /// <summary>
    /// Whether to receive crime-related hazard alerts.
    /// </summary>
    public bool CrimeAlertsEnabled { get; set; } = true;

    /// <summary>
    /// Whether to receive traffic accident hazard alerts.
    /// </summary>
    public bool AccidentAlertsEnabled { get; set; } = true;

    /// <summary>
    /// Whether to receive infrastructure hazard alerts.
    /// </summary>
    public bool InfrastructureAlertsEnabled { get; set; } = true;

    /// <summary>
    /// Whether all hazard categories should be enabled regardless of individual toggles.
    /// </summary>
    public bool AllHazardAlertsEnabled { get; set; } = false;

    /// <summary>
    /// Radius used for proximity-based hazard alerts.
    /// </summary>
    public int AlertRadiusKm { get; set; } = 5;

    /// <summary>
    /// Alert severity threshold: All, High, or Critical.
    /// </summary>
    public string HazardSeverityThreshold { get; set; } = "All";

    /// <summary>
    /// Profile visibility mode: public or private.
    /// </summary>
    public string ProfileVisibility { get; set; } = "public";

    /// <summary>
    /// Whether the user account is banned
    /// </summary>
    public bool IsBanned { get; set; } = false;

    /// <summary>
    /// Monotonic counter used to rotate and revoke refresh tokens.
    /// </summary>
    public int RefreshTokenVersion { get; set; } = 0;

    /// <summary>
    /// Timestamp of the most recent refresh token issuance.
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? LastRefreshTokenIssuedAt { get; set; }

    /// <summary>
    /// When the user was banned (if applicable)
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? BannedAt { get; set; }

    /// <summary>
    /// Reason for banning the user
    /// </summary>
    public string? BannedReason { get; set; }

    /// <summary>
    /// Whether the user's account is restricted
    /// </summary>
    public bool IsRestricted { get; set; } = false;

    /// <summary>
    /// When the user restriction expires
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? RestrictedUntil { get; set; }

    /// <summary>
    /// Reason for restricting the user
    /// </summary>
    public string? RestrictionReason { get; set; }

    /// <summary>
    /// When the user account was created
    /// </summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
