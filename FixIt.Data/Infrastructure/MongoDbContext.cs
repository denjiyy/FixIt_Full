using MongoDB.Driver;
using FixIt.Models.Users;
using FixIt.Models.Issues;
using FixIt.Models.Engagement;
using FixIt.Models.Locations;
using FixIt.Models.Moderation;
using FixIt.Models.Transparency;
using FixIt.Models.Accessibility;
using TagModel = FixIt.Models.Issues.Tag;

namespace FixIt.Models.Infrastructure;

public class MongoDbContext
{
    public IMongoDatabase Database { get; }

    // Identity and User Management
    public IMongoCollection<ApplicationUser> Users => Database.GetCollection<ApplicationUser>(MongoCollectionNames.Users);

    // Issue Management
    public IMongoCollection<Issue> Issues => Database.GetCollection<Issue>(MongoCollectionNames.Issues);
    public IMongoCollection<TagModel> Tags => Database.GetCollection<TagModel>(MongoCollectionNames.Tags);
    public IMongoCollection<OfficialResponse> OfficialResponses => Database.GetCollection<OfficialResponse>(MongoCollectionNames.OfficialResponses);

    // Engagement
    public IMongoCollection<Comment> Comments => Database.GetCollection<Comment>(MongoCollectionNames.Comments);
    public IMongoCollection<Vote> Votes => Database.GetCollection<Vote>(MongoCollectionNames.Votes);

    // Media Management
    public IMongoCollection<FixIt.Models.Media.Media> Media => Database.GetCollection<FixIt.Models.Media.Media>(MongoCollectionNames.Media);
    public IMongoCollection<FixIt.Models.Media.MediaReference> MediaReferences => Database.GetCollection<FixIt.Models.Media.MediaReference>(MongoCollectionNames.MediaReferences);

    // Location Data
    public IMongoCollection<City> Cities => Database.GetCollection<City>(MongoCollectionNames.Cities);
    public IMongoCollection<Neighborhood> Neighborhoods => Database.GetCollection<Neighborhood>(MongoCollectionNames.Neighborhoods);

    // Moderation
    public IMongoCollection<ModerationAction> ModerationActions => Database.GetCollection<ModerationAction>(MongoCollectionNames.ModerationActions);
    public IMongoCollection<ContentReport> ContentReports => Database.GetCollection<ContentReport>(MongoCollectionNames.ContentReports);

    // Issue Resolution Evidence (Before/After Photo System)
    public IMongoCollection<IssueResolutionEvidence> IssueResolutionEvidence => Database.GetCollection<IssueResolutionEvidence>(MongoCollectionNames.IssueResolutionEvidence);


    public IMongoCollection<TranslationRecord> Translations => Database.GetCollection<TranslationRecord>(MongoCollectionNames.Translations);
    public IMongoCollection<SupportedLanguage> SupportedLanguages => Database.GetCollection<SupportedLanguage>(MongoCollectionNames.SupportedLanguages);

    public MongoDbContext(IMongoClient client, string databaseName)
    {
        Database = client.GetDatabase(databaseName);
    }
}
