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

        // Required for ASP.NET Identity
        var normalizedUsernameIndex = new CreateIndexModel<ApplicationUser>(
            Builders<ApplicationUser>.IndexKeys.Ascending(u => u.NormalizedUserName),
            new CreateIndexOptions { Unique = true, Name = "ux_users_normalizedusername" }
        );
        await users.Indexes.CreateOneAsync(normalizedUsernameIndex);

        var normalizedEmailIndex = new CreateIndexModel<ApplicationUser>(
            Builders<ApplicationUser>.IndexKeys.Ascending(u => u.NormalizedEmail),
            new CreateIndexOptions { Name = "ix_users_normalizedemail" }
        );
        await users.Indexes.CreateOneAsync(normalizedEmailIndex);
    }
}
