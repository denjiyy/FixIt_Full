using System.Net.Http.Json;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Common;
using FixIt.Models.Issues;
using FixIt.Services.AI;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using Xunit;

namespace FixIt.Tests.Integration;

/// <summary>
/// End-to-end coverage for the pre-submit duplicate reminder: seeds issues in an
/// isolated (GUID) city, then hits POST /api/analysis/duplicate-check and asserts
/// the nearby keyword match comes back while the far-away and unrelated ones don't.
/// Exercises routing, DI, the service, and JSON serialization together.
/// </summary>
public class DuplicateCheckIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public DuplicateCheckIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private void RequireDocker() => Skip.IfNot(_fixture.IsAvailable,
        $"Docker testcontainer unavailable. {_fixture.UnavailabilityReason ?? "(no reason captured)"}");

    [SkippableFact]
    public async Task DuplicateCheck_ReturnsNearbyKeywordMatch_ExcludingFarAndUnrelated()
    {
        RequireDocker();

        // Unique city isolates these issues from anything the seeder created.
        // CityId is stored as an ObjectId, so it must be a valid 24-hex string.
        var cityId = ObjectId.GenerateNewId().ToString();

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRepository<Issue>>();
            await repo.InsertAsync(MakeIssue("Large pothole on Vitosha Blvd damaging cars",
                "Deep pothole near the tram stop.", cityId, 42.6901, 23.3201)); // nearby match
            await repo.InsertAsync(MakeIssue("Large pothole on Vitosha Blvd damaging cars",
                "Deep pothole near the tram stop.", cityId, 42.7500, 23.3200)); // ~6.7 km — out of radius
            await repo.InsertAsync(MakeIssue("Broken streetlight on Cherni Vrah",
                "Unlit crossing at night.", cityId, 42.6902, 23.3202)); // nearby but unrelated
        }

        var client = _fixture.Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/analysis/duplicate-check", new
        {
            title = "Huge pothole on Vitosha Boulevard, cars getting damaged",
            description = "The pothole by the tram stop keeps growing.",
            latitude = 42.6900,
            longitude = 23.3200,
            cityId,
        });

        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<List<SimilarIssueResult>>();

        Assert.NotNull(results);
        var match = Assert.Single(results!);
        Assert.Contains("pothole", match.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pothole", match.MatchedKeywords);
        Assert.False(string.IsNullOrEmpty(match.IssueId), "Result must carry an issue id for the link.");
        Assert.InRange(match.DistanceKm, 0, 5);
    }

    private static Issue MakeIssue(string title, string description, string cityId, double lat, double lng) => new()
    {
        Id = ObjectId.GenerateNewId().ToString(),
        Title = title,
        Description = description,
        CityId = cityId,
        Reporter = new UserSummary { Id = ObjectId.GenerateNewId().ToString(), DisplayName = "Tester" },
        Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
            new GeoJson2DGeographicCoordinates(lng, lat)),
    };
}
