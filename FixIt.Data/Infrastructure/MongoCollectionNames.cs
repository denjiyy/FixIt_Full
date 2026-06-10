namespace FixIt.Models.Infrastructure;

public static class MongoCollectionNames
{
    // MUST match the collection AspNetCore.Identity.Mongo writes users to. That
    // library defaults to "Users" (it is not configured with a custom
    // UsersCollection), so IRepository<ApplicationUser> — and MongoDbContext /
    // UserConfiguration — must read the same collection. A prior value of
    // "AspNetUsers" pointed every IRepository<ApplicationUser> lookup at an empty
    // collection, so e.g. HazardService.ResolveHazardAsync's admin re-check always
    // failed (no user found → Forbidden).
    public const string Users = "Users";
    public const string Cities = "cities";
    public const string Neighborhoods = "neighborhoods";
    public const string Tags = "tags";
    public const string Issues = "issues";
    public const string Votes = "votes";
    public const string ViewEvents = "viewEvents";
    public const string UserReputations = "userReputations";
    public const string ReputationTransactions = "reputationTransactions";
    public const string Leaderboards = "leaderboards";
    public const string Hazards = "hazards";
    public const string IssueAnalyses = "issueAnalyses";
    public const string AdminSuggestions = "adminSuggestions";
    public const string Media = "media";
    public const string MediaReferences = "mediaReferences";
    public const string Comments = "comments";
    public const string ContentReports = "contentReports";
    public const string IssueResolutionEvidence = "issueResolutionEvidence";
    public const string Translations = "translations";
    public const string SupportedLanguages = "supportedLanguages";
    public const string OfficialResponses = "officialResponses";
    public const string ModerationActions = "moderationActions";
}
