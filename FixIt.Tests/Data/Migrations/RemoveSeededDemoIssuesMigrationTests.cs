using FixIt.Data.Infrastructure.Migrations;
using MongoDB.Bson;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Xunit;

namespace FixIt.Tests.Data.Migrations;

/// <summary>
/// Confirms <see cref="Migration_20260603_001_RemoveSeededDemoIssues"/> deletes the
/// synthetic "Civic Reporter" demo issues and leaves genuine user issues alone —
/// including the adversarial case of a real user who shares the display name but has
/// a normal (non-DiceBear) avatar. Also covers idempotency and the production gate.
/// </summary>
public class RemoveSeededDemoIssuesMigrationTests : IAsyncLifetime
{
    private MongoDbContainer? _mongo;
    private MongoClient? _client;
    private bool _available;
    private string? _reason;

    private const string SeedAvatar = "https://avatars.dicebear.com/api/avataaars/CivicReporter.svg";

    public async Task InitializeAsync()
    {
        try
        {
            _mongo = new MongoDbBuilder().WithImage("mongo:7.0").Build();
            await _mongo.StartAsync();
            _client = new MongoClient(_mongo.GetConnectionString());
            _available = true;
        }
        catch (Exception ex)
        {
            _reason = $"{ex.GetType().Name}: {ex.Message}";
            _available = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_mongo != null)
        {
            await _mongo.DisposeAsync();
        }
    }

    private IMongoDatabase FreshDb() => _client!.GetDatabase($"mig_{Guid.NewGuid():N}");

    [SkippableFact]
    public async Task RemoveSeededDemoIssues_DeletesSeeded_AndSparesRealOnes()
    {
        Skip.IfNot(_available, $"Docker testcontainer unavailable. {_reason}");

        var db = FreshDb();
        var issues = db.GetCollection<BsonDocument>("issues");
        await issues.InsertManyAsync(new[]
        {
            SeededIssue("Large pothole on Vasil Levski Boulevard"),
            SeededIssue("Excessive garbage accumulation near City Garden"),
            RealIssue("My street light is out", "Jane Citizen", "https://res.cloudinary.com/x/a.jpg"),
            RealIssue("Broken bench", "Ivan Petrov", null),
            // Adversarial: a real user who picked the same display name but has a
            // normal avatar. Must NOT be deleted — the DiceBear avatar is the key.
            RealIssue("Genuine report", "Civic Reporter", "https://res.cloudinary.com/x/real.jpg"),
        });

        var removed = await Migration_20260603_001_RemoveSeededDemoIssues.RemoveSeededDemoIssuesAsync(db);

        Assert.Equal(2, removed);
        var remaining = await issues.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        Assert.Equal(3, remaining.Count);
        Assert.DoesNotContain(remaining, d =>
        {
            var avatar = d["Reporter"]["AvatarUrl"];
            return avatar.IsString && avatar.AsString.Contains("dicebear");
        });
        Assert.Contains(remaining, d => d["Title"].AsString == "Genuine report");

        // Idempotent: a second run removes nothing.
        Assert.Equal(0, await Migration_20260603_001_RemoveSeededDemoIssues.RemoveSeededDemoIssuesAsync(db));
    }

    private static BsonDocument SeededIssue(string title) => new()
    {
        { "Title", title },
        { "Reporter", new BsonDocument
            {
                { "Id", ObjectId.GenerateNewId() },
                { "DisplayName", "Civic Reporter" },
                { "AvatarUrl", SeedAvatar },
            }
        },
    };

    private static BsonDocument RealIssue(string title, string displayName, string? avatarUrl) => new()
    {
        { "Title", title },
        { "Reporter", new BsonDocument
            {
                { "Id", ObjectId.GenerateNewId() },
                { "DisplayName", displayName },
                { "AvatarUrl", avatarUrl is null ? BsonNull.Value : avatarUrl },
            }
        },
    };
}
