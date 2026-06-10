using MongoDB.Driver;
using FixIt.Data.Configuration.Contracts;
using FixIt.Models.Users;
using FixIt.Models.Infrastructure;

namespace FixIt.Data.Configuration;

public class UserConfiguration : ICollectionConfigurator
{
    public async Task ConfigureAsync(IMongoDatabase db, bool seedDemoData)
    {
        var users = db.GetCollection<ApplicationUser>(MongoCollectionNames.Users);

        // This is the same collection AspNetCore.Identity.Mongo manages, so an
        // equivalent index may already exist with different options/name. Treat a
        // create conflict as benign rather than failing startup.
        var normalizedUsernameIndex = new CreateIndexModel<ApplicationUser>(
            Builders<ApplicationUser>.IndexKeys.Ascending(u => u.NormalizedUserName),
            new CreateIndexOptions { Unique = true, Name = "ux_users_normalizedusername" }
        );

        var normalizedEmailIndex = new CreateIndexModel<ApplicationUser>(
            Builders<ApplicationUser>.IndexKeys.Ascending(u => u.NormalizedEmail),
            new CreateIndexOptions { Name = "ix_users_normalizedemail" }
        );

        try
        {
            await users.Indexes.CreateOneAsync(normalizedUsernameIndex);
            await users.Indexes.CreateOneAsync(normalizedEmailIndex);
        }
        catch (MongoCommandException ex) when (ex.CodeName is "IndexOptionsConflict" or "IndexKeySpecsConflict")
        {
            // An equivalent index already exists (created by Identity); fine.
        }
    }
}
