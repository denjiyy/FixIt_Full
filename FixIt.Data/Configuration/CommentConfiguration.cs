using MongoDB.Driver;
using FixIt.Data.Configuration.Contracts;
using FixIt.Models.Engagement;

namespace FixIt.Data.Configuration;

public class CommentConfiguration : ICollectionConfigurator
{
    public async Task ConfigureAsync(IMongoDatabase db, bool seedDemoData)
    {
        var comments = db.GetCollection<Comment>("comments");

        // For showing comments on an issue
        var issueIndex = new CreateIndexModel<Comment>(
            Builders<Comment>.IndexKeys
                .Ascending(c => c.IssueId)
                .Descending(c => c.CreatedAt),
            new CreateIndexOptions { Name = "idx_comments_issue_created" }
        );
        await comments.Indexes.CreateOneAsync(issueIndex);

        // For finding user's comments
        var authorIndex = new CreateIndexModel<Comment>(
            Builders<Comment>.IndexKeys.Ascending(c => c.AuthorId),
            new CreateIndexOptions { Name = "idx_comments_author" }
        );
        await comments.Indexes.CreateOneAsync(authorIndex);
    }
}