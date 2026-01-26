using MongoDB.Driver;
using MongoDB.Bson;
using FixIt.Data.Configuration.Contracts;
using FixIt.Models.Issues;

namespace FixIt.Data.Configuration;

public class IssueConfiguration : ICollectionConfigurator
{
    public async Task ConfigureAsync(IMongoDatabase db)
    {
        var issues = db.GetCollection<Issue>("issues");

        // Geospatial index for "find issues near me"
        var geoIndex = new CreateIndexModel<Issue>(
            Builders<Issue>.IndexKeys.Geo2DSphere(i => i.Location),
            new CreateIndexOptions { Name = "idx_issues_location_2dsphere" }
        );
        await issues.Indexes.CreateOneAsync(geoIndex);

        // Text search index for title and description
        var textIndex = new CreateIndexModel<Issue>(
            Builders<Issue>.IndexKeys
                .Text(i => i.Title)
                .Text(i => i.Description),
            new CreateIndexOptions { Name = "idx_issues_text" }
        );
        await issues.Indexes.CreateOneAsync(textIndex);

        // Compound index for common queries
        var cityStatusIndex = new CreateIndexModel<Issue>(
            Builders<Issue>.IndexKeys
                .Ascending(i => i.CityId)
                .Ascending(i => i.Status)
                .Descending(i => i.CreatedAt),
            new CreateIndexOptions { Name = "idx_issues_city_status_created" }
        );
        await issues.Indexes.CreateOneAsync(cityStatusIndex);

        // Index for user's issues
        var reporterIndex = new CreateIndexModel<Issue>(
            Builders<Issue>.IndexKeys.Ascending("Reporter.Id"),
            new CreateIndexOptions { Name = "idx_issues_reporter_id" }
        );
        await issues.Indexes.CreateOneAsync(reporterIndex);

        // Partial index for active issues
        var command = new BsonDocument
        {
            { "createIndexes", "issues" },
            { "indexes", new BsonArray
                {
                    new BsonDocument
                    {
                        { "name", "idx_issues_active" },
                        { "key", new BsonDocument("CreatedAt", -1) },
                        { "partialFilterExpression", new BsonDocument("IsDeleted", false) }
                    }
                }
            }
        };
        await db.RunCommandAsync<BsonDocument>(command);
    }
}