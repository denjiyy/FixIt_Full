using MongoDB.Driver;

namespace FixIt.Data.Infrastructure.Migrations;

/// <summary>
/// Example Migration: Create collection indexes
/// Runs once on application startup
/// Idempotent: Safe to run multiple times
/// </summary>
public class Migration_20240101_001_CreateIndexes : IMigration
{
    public string Version => "20240101_001";
    public string Description => "Create collection indexes for performance";

    public async Task UpAsync(IMongoDatabase database)
    {
        // Index for Issues collection
        var issuesCollection = database.GetCollection("issues");
        var issueIndexes = new List<CreateIndexModel<BsonDocument>>
        {
            // Query by CityId
            new(Builders<BsonDocument>.IndexKeys.Ascending("cityId")),
            // Query by Category  
            new(Builders<BsonDocument>.IndexKeys.Ascending("category")),
            // Geospatial index for location queries
            new(Builders<BsonDocument>.IndexKeys.Geo2DSphere("location")),
            // Composite index for status + CreatedDate (sorting)
            new(Builders<BsonDocument>.IndexKeys.Ascending("status").Ascending("createdDate")),
        };
        
        try
        {
            await issuesCollection.Indexes.CreateManyAsync(issueIndexes);
        }
        catch (MongoCommandException ex) when (ex.Code == 68) // IndexAlreadyExists
        {
            // Index already exists, that's fine
        }

        // Index for Comments collection
        var commentsCollection = database.GetCollection("comments");
        var commentIndexes = new List<CreateIndexModel<BsonDocument>>
        {
            new(Builders<BsonDocument>.IndexKeys.Ascending("issueId")),
            new(Builders<BsonDocument>.IndexKeys.Ascending("authorId")),
        };

        try
        {
            await commentsCollection.Indexes.CreateManyAsync(commentIndexes);
        }
        catch (MongoCommandException ex) when (ex.Code == 68)
        {
            // Index already exists, that's fine
        }

        // Index for Users collection
        var usersCollection = database.GetCollection("users");
        try
        {
            await usersCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("email"),
                    new CreateIndexOptions { Unique = true }
                )
            );
        }
        catch (MongoCommandException ex) when (ex.Code == 68)
        {
            // Index already exists
        }
    }
}
