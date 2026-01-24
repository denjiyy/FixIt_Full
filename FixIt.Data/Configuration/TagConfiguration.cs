using MongoDB.Driver;
using FixIt.Data.Configuration.Contracts;

namespace FixIt.Data.Configuration;

public class TagConfiguration : ICollectionConfigurator
{
    public async Task ConfigureAsync(IMongoDatabase db)
    {
        var tags = db.GetCollection<FixIt.Models.Issues.Tag>("tags");

        var uniqueIndex = new CreateIndexModel<FixIt.Models.Issues.Tag>(
            Builders<FixIt.Models.Issues.Tag>.IndexKeys.Ascending(t => t.Name),
            new CreateIndexOptions { Unique = true, Name = "ux_tags_name" }
        );
        await tags.Indexes.CreateOneAsync(uniqueIndex);

        var usageIndex = new CreateIndexModel<FixIt.Models.Issues.Tag>(
            Builders<FixIt.Models.Issues.Tag>.IndexKeys.Descending(t => t.UsageCount),
            new CreateIndexOptions { Name = "ix_tags_usage" }
        );
        await tags.Indexes.CreateOneAsync(usageIndex);

        var seed = new[]
        {
                new FixIt.Models.Issues.Tag { Name = "pothole", Category = "Roads", UsageCount = 0, IsApproved = true },
                new FixIt.Models.Issues.Tag { Name = "street-light", Category = "Lighting", UsageCount = 0, IsApproved = true },
                new FixIt.Models.Issues.Tag { Name = "garbage", Category = "Sanitation", UsageCount = 0, IsApproved = true }
            };

        foreach (var t in seed)
        {
            var filter = Builders<FixIt.Models.Issues.Tag>.Filter.Eq(tg => tg.Name, t.Name);
            var options = new ReplaceOptions { IsUpsert = true };
            await tags.ReplaceOneAsync(filter, t, options);
        }
    }
}

