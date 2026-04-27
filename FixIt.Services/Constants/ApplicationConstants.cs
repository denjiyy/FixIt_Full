namespace FixIt.Services.Constants;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string User = "User";
    public const string AdminOrModerator = $"{Admin},{Moderator}";
    public const string ModeratorOrAdmin = $"{Moderator},{Admin}";
}

public static class PolicyNames
{
    public const string AdminOnly = "AdminOnly";
}

public static class RateLimitPolicyNames
{
    public const string AuthStrict = "auth-strict";
    public const string Api = "api";
    public const string Reporting = "reporting";
    public const string Upload = "upload";
}

public static class CustomClaimTypes
{
    public const string DisplayName = "DisplayName";
    public const string ReputationScore = "ReputationScore";
    public const string TrustLevel = "TrustLevel";
    public const string IsVerifiedOfficial = "IsVerifiedOfficial";
    public const string IsBanned = "IsBanned";
    public const string IsRestricted = "IsRestricted";
    public const string OfficialTitle = "OfficialTitle";
    public const string OfficialDepartment = "OfficialDepartment";
}
