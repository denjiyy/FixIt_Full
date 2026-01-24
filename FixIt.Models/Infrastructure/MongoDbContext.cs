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

    public IMongoCollection<ApplicationUser> Users => Database.GetCollection<ApplicationUser>("AspNetUsers");
    public IMongoCollection<Issue> Issues => Database.GetCollection<Issue>("issues");
    public IMongoCollection<Comment> Comments => Database.GetCollection<Comment>("comments");
    public IMongoCollection<Vote> Votes => Database.GetCollection<Vote>("votes");
    public IMongoCollection<FixIt.Models.Media.Media> Media => Database.GetCollection<FixIt.Models.Media.Media>("media");
    public IMongoCollection<City> Cities => Database.GetCollection<City>("cities");
    public IMongoCollection<Neighborhood> Neighborhoods => Database.GetCollection<Neighborhood>("neighborhoods");
    public IMongoCollection<NotificationSubscription> Subscriptions => Database.GetCollection<NotificationSubscription>("subscriptions");
    public IMongoCollection<ModerationAction> ModerationActions => Database.GetCollection<ModerationAction>("moderation_actions");
    public IMongoCollection<AuditLog> AuditLogs => Database.GetCollection<AuditLog>("audit_logs");
    public IMongoCollection<AnalyticsEvent> AnalyticsEvents => Database.GetCollection<AnalyticsEvent>("analytics_events");

    public MongoDbContext(IMongoClient client, string databaseName)
    {
        Database = client.GetDatabase(databaseName);
    }
}
