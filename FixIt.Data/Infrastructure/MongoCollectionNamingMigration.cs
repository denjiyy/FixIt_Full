using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FixIt.Models.Infrastructure;

public static class MongoCollectionNamingMigration
{
    private static readonly IReadOnlyDictionary<string, string> LegacyToCanonicalMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["media_references"] = MongoCollectionNames.MediaReferences,
            ["content_reports"] = MongoCollectionNames.ContentReports,
            ["official_responses"] = MongoCollectionNames.OfficialResponses,
            ["moderation_actions"] = MongoCollectionNames.ModerationActions
        };

    public static async Task RunAsync(IMongoDatabase database, ILogger logger, CancellationToken cancellationToken = default)
    {
        var collectionNames = await (await database.ListCollectionNamesAsync(cancellationToken: cancellationToken))
            .ToListAsync(cancellationToken);

        var existingCollections = new HashSet<string>(collectionNames, StringComparer.Ordinal);
        var databaseName = database.DatabaseNamespace.DatabaseName;

        foreach (var (legacyName, canonicalName) in LegacyToCanonicalMap)
        {
            if (!existingCollections.Contains(legacyName))
            {
                continue;
            }

            if (existingCollections.Contains(canonicalName))
            {
                logger.LogWarning(
                    "Collection rename skipped because both names exist: {LegacyCollection} and {CanonicalCollection}.",
                    legacyName,
                    canonicalName);
                continue;
            }

            try
            {
                var adminDatabase = database.Client.GetDatabase("admin");
                var command = new BsonDocument
                {
                    { "renameCollection", $"{databaseName}.{legacyName}" },
                    { "to", $"{databaseName}.{canonicalName}" },
                    { "dropTarget", false }
                };

                await adminDatabase.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken);
                existingCollections.Remove(legacyName);
                existingCollections.Add(canonicalName);

                logger.LogInformation(
                    "Renamed MongoDB collection from {LegacyCollection} to {CanonicalCollection}.",
                    legacyName,
                    canonicalName);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to rename collection from {LegacyCollection} to {CanonicalCollection}.",
                    legacyName,
                    canonicalName);
            }
        }
    }
}
