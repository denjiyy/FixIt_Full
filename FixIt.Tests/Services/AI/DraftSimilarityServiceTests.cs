using System.Linq.Expressions;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.AI;
using FixIt.Models.Enums;
using FixIt.Models.Issues;
using FixIt.Services.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver.GeoJsonObjectModel;
using Moq;
using Xunit;

namespace FixIt.Tests.Services.AI;

/// <summary>
/// Service-level coverage for the pre-submit duplicate reminder: candidates are
/// fetched for the draft's city, then ranked/filtered by keyword overlap within the
/// 5 km radius and mapped to link-ready results (id, matched keywords, distance,
/// status). The repository is mocked so the test stays deterministic and fast.
/// </summary>
public class DraftSimilarityServiceTests
{
    private static Issue MakeIssue(string id, string title, string description,
        double lat, double lng, IssueStatus status = IssueStatus.New)
        => new()
        {
            Id = id,
            Title = title,
            Description = description,
            CityId = "sofia",
            Status = status,
            Category = IssueCategory.Infrastructure,
            Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                new GeoJson2DGeographicCoordinates(lng, lat)),
        };

    private static OpenAIIssueAnalysisService BuildService(IReadOnlyCollection<Issue> candidates)
    {
        var issueRepo = new Mock<IRepository<Issue>>();
        issueRepo
            .Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Issue, bool>>>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new PagedResult<Issue> { Items = candidates, Total = candidates.Count });

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        return new OpenAIIssueAnalysisService(
            new Mock<IRepository<IssueAnalysis>>().Object,
            issueRepo.Object,
            new HttpClient(),
            config,
            NullLogger<OpenAIIssueAnalysisService>.Instance);
    }

    [Fact]
    public async Task FindSimilarIssuesForDraft_ReturnsNearbyKeywordMatch_WithLinkKeywordsAndStatus()
    {
        var near = MakeIssue("NEAR", "Large pothole on Vitosha Blvd damaging cars",
            "Deep pothole near the tram stop.", 42.6901, 23.3201, IssueStatus.Confirmed);
        var farMatch = MakeIssue("FAR", "Large pothole on Vitosha Blvd damaging cars",
            "Deep pothole near the tram stop.", 42.7500, 23.3200); // ~6.7 km away
        var unrelated = MakeIssue("UNREL", "Broken streetlight on Cherni Vrah",
            "Unlit crossing at night.", 42.6902, 23.3202);

        var service = BuildService(new[] { near, farMatch, unrelated });

        var results = await service.FindSimilarIssuesForDraftAsync(new DraftSimilarityQuery
        {
            Title = "Huge pothole on Vitosha Boulevard, cars getting damaged",
            Description = "The pothole by the tram stop keeps growing.",
            Latitude = 42.6900,
            Longitude = 23.3200,
            CityId = "sofia",
            RadiusKm = 5,
        });

        var match = Assert.Single(results);
        Assert.Equal("NEAR", match.IssueId);
        Assert.Contains("pothole", match.MatchedKeywords);
        Assert.Equal("Confirmed", match.Status);
        Assert.InRange(match.DistanceKm, 0, 5);
    }

    [Fact]
    public async Task FindSimilarIssuesForDraft_WithoutCity_ReturnsEmpty()
    {
        var service = BuildService(Array.Empty<Issue>());

        var results = await service.FindSimilarIssuesForDraftAsync(new DraftSimilarityQuery
        {
            Title = "Pothole",
            Description = "Deep pothole damaging cars",
            Latitude = 42.69,
            Longitude = 23.32,
            CityId = null,
        });

        Assert.Empty(results);
    }
}
