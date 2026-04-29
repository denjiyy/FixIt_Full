using MongoDB.Bson;
using MongoDB.Driver;

namespace FixIt.Data.Infrastructure.Migrations;

/// <summary>
/// Example Migration: Add validated field to issues
/// Demonstrates data structure changes across existing documents
/// </summary>
public class Migration_20240103_001_AddIssueSafetyValidation : IMigration
{
    public string Version => "20240103_001";
    public string Description => "Add safety validation tracking to issues";

    public async Task UpAsync(IMongoDatabase database)
    {
        var issuesCollection = database.GetCollection<BsonDocument>("issues");

        try
        {
            // Add 'validated' field to any document that doesn't have it
            // Set to false for existing documents (requires manual review)
            var updateDefinition = Builders<BsonDocument>.Update
                .SetOnInsert("validated", false)
                .SetOnInsert("validatedAt", BsonNull.Value)
                .SetOnInsert("validatedBy", BsonNull.Value);

            var filter = Builders<BsonDocument>.Filter
                .Exists("validated", false);

            await issuesCollection.UpdateManyAsync(filter, updateDefinition);
        }
        catch (Exception ex)
        {
            // Log but don't fail - this is a non-critical schema update
            throw new InvalidOperationException(
                $"Failed to add 'validated' field to issues: {ex.Message}", ex);
        }
    }
}
