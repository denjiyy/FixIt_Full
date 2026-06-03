using MongoDB.Bson;
using MongoDB.Driver;

namespace FixIt.Data.Infrastructure.Migrations;

/// <summary>
/// Removes legacy demo/sample issues that were seeded into the production database
/// before the production seed gating (seedDemoData=false) existed — e.g. an early
/// deploy that ran before <c>ASPNETCORE_ENVIRONMENT=Production</c> was set, so the
/// app treated itself as non-production and seeded sample issues. Production must
/// only ever contain user-submitted issues.
///
/// Scoped to production on purpose: demo issues are an intentional convenience in
/// development (seeded by IssueConfiguration), so non-production databases are left
/// untouched. The migration is still recorded as applied everywhere, so it never
/// re-runs.
///
/// Seeded issues are identified by their synthetic reporter: the fixed display name
/// "Civic Reporter" together with a DiceBear avatar URL. "dicebear" appears nowhere
/// in the codebase except the old IssueConfiguration seed, and no real user flow
/// assigns a DiceBear avatar, so this signature can never match a genuine report.
///
/// Idempotent: once the database is clean, re-running deletes nothing.
/// </summary>
public class Migration_20260603_001_RemoveSeededDemoIssues : IMigration
{
    public string Version => "20260603_001";

    public string Description => "Remove legacy seeded demo issues (synthetic 'Civic Reporter' author) in production";

    // The synthetic author the old seeder stamped on every sample issue.
    private const string SeedReporterDisplayName = "Civic Reporter";

    public async Task UpAsync(IMongoDatabase database)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                $"[Migration 20260603_001] Environment '{environment ?? "(unset)"}' is not Production; " +
                "leaving development demo issues in place.");
            return;
        }

        var removed = await RemoveSeededDemoIssuesAsync(database);
        Console.WriteLine(removed == 0
            ? "[Migration 20260603_001] No seeded demo issues found in production; nothing to remove."
            : $"[Migration 20260603_001] Removed {removed} seeded demo issue(s) from production.");
    }

    /// <summary>
    /// Deletes the synthetic seed issues regardless of environment. Exposed so the
    /// deletion + safety behaviour can be asserted directly in tests, independent of
    /// the production gate in <see cref="UpAsync"/>.
    /// </summary>
    public static async Task<long> RemoveSeededDemoIssuesAsync(IMongoDatabase database)
    {
        var issues = database.GetCollection<BsonDocument>("issues");

        // Both conditions must hold: the synthetic display name AND a DiceBear
        // avatar. DiceBear URLs are only ever produced by the seed, so this is a
        // precise signature for demo data that never matches a real report.
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("Reporter.DisplayName", SeedReporterDisplayName),
            Builders<BsonDocument>.Filter.Regex("Reporter.AvatarUrl", new BsonRegularExpression("dicebear", "i")));

        var result = await issues.DeleteManyAsync(filter);
        return result.DeletedCount;
    }
}
