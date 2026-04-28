using MongoDB.Driver;

namespace FixIt.Data.Infrastructure.Migrations;

/// <summary>
/// Example Migration: Add TTL index for session data
/// Automatically deletes documents after expiration
/// </summary>
public class Migration_20240102_001_AddSessionTtl : IMigration
{
    public string Version => "20240102_001";
    public string Description => "Add TTL index for session collection";

    public async Task UpAsync(IMongoDatabase database)
    {
        // Create sessions collection if it doesn't exist
        var collectionNames = await database.ListCollectionNamesAsync();
        var collections = await collectionNames.ToListAsync();

        if (!collections.Contains("sessions"))
        {
            await database.CreateCollectionAsync("sessions");
        }

        var sessionsCollection = database.GetCollection<BsonDocument>("sessions");

        try
        {
            // TTL index: documents expire 24 hours after last update
            await sessionsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("lastActivityAt"),
                    new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(24) }
                )
            );
        }
        catch (MongoCommandException ex) when (ex.Code == 68)
        {
            // Index already exists
        }
    }
}
