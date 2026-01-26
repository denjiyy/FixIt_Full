using MongoDB.Driver;
using FixIt.Data.Configuration.Contracts;
using FixIt.Models.Engagement;

namespace FixIt.Data.Configuration;

public class VoteConfiguration : ICollectionConfigurator
{
    public async Task ConfigureAsync(IMongoDatabase db)
    {
        var votes = db.GetCollection<Vote>("votes");

        // One vote per user per issue
        var uniqueIndex = new CreateIndexModel<Vote>(
            Builders<Vote>.IndexKeys
                .Ascending(v => v.IssueId)
                .Ascending(v => v.UserId),
            new CreateIndexOptions { Unique = true, Name = "ux_votes_issue_user" }
        );
        await votes.Indexes.CreateOneAsync(uniqueIndex);
    }
}