using MongoDB.Driver;
using FixIt.Models.Users;
using FixIt.Models.Issues;
using FixIt.Models.Engagement;
using FixIt.Models.Locations;
using FixIt.Models.Moderation;
using FixIt.Models.Notifications;
using FixIt.Models.Auditing;
using FixIt.Models.Analytics;

namespace FixIt.Models.Infrastructure;

public class MongoDbContext
{
    public IMongoDatabase Database { get; }

    // Identity and User Management
    public IMongoCollection<ApplicationUser> Users => Database.GetCollection<ApplicationUser>("AspNetUsers");
    public IMongoCollection<UserReputation> UserReputations => Database.GetCollection<UserReputation>("user_reputations");
    public IMongoCollection<RateLimitEntry> RateLimits => Database.GetCollection<RateLimitEntry>("rate_limits");

    // Issue Management
    public IMongoCollection<Issue> Issues => Database.GetCollection<Issue>("issues");
    public IMongoCollection<Tag> Tags => Database.GetCollection<Tag>("tags");
    public IMongoCollection<OfficialResponse> OfficialResponses => Database.GetCollection<OfficialResponse>("official_responses");

    // Engagement
    public IMongoCollection<Comment> Comments => Database.GetCollection<Comment>("comments");
    public IMongoCollection<Vote> Votes => Database.GetCollection<Vote>("votes");

    // Media Management
    public IMongoCollection<FixIt.Models.Media.Media> Media => Database.GetCollection<FixIt.Models.Media.Media>("media");
    public IMongoCollection<FixIt.Models.Media.MediaReference> MediaReferences => Database.GetCollection<FixIt.Models.Media.MediaReference>("media_references");

    // Location Data
    public IMongoCollection<City> Cities => Database.GetCollection<City>("cities");
    public IMongoCollection<Neighborhood> Neighborhoods => Database.GetCollection<Neighborhood>("neighborhoods");

    // Notifications
    public IMongoCollection<NotificationSubscription> Subscriptions => Database.GetCollection<NotificationSubscription>("subscriptions");
    public IMongoCollection<Notification> Notifications => Database.GetCollection<Notification>("notifications");

    // Moderation
    public IMongoCollection<ModerationAction> ModerationActions => Database.GetCollection<ModerationAction>("moderation_actions");
    public IMongoCollection<ContentReport> ContentReports => Database.GetCollection<ContentReport>("content_reports");

    // Auditing and Analytics
    public IMongoCollection<AuditLog> AuditLogs => Database.GetCollection<AuditLog>("audit_logs");
    public IMongoCollection<AnalyticsEvent> AnalyticsEvents => Database.GetCollection<AnalyticsEvent>("analytics_events");

    public MongoDbContext(IMongoClient client, string databaseName)
    {
        Database = client.GetDatabase(databaseName);
    }
}