using FixIt.Models.AI;
using FixIt.Models.Enums;
using FixIt.Models.Issues;
using FixIt.Services.AI;
using MongoDB.Driver.GeoJsonObjectModel;
using Xunit;

namespace FixIt.Tests.Services.AI;

/// <summary>
/// Tests for the local (no-OpenAI) duplicate-detection pass — the feature that was
/// previously a no-op because <see cref="IssueAnalysis.PotentialDuplicates"/> was never populated.
/// </summary>
public class DuplicateDetectionTests
{
    private static Issue MakeIssue(
        string id, string title, string description, IssueCategory? category,
        double lat, double lng, string cityId = "sofia", IssueStatus status = IssueStatus.New)
        => new()
        {
            Id = id,
            Title = title,
            Description = description,
            Category = category,
            CityId = cityId,
            Status = status,
            Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                new GeoJson2DGeographicCoordinates(lng, lat)),
        };

    [Fact]
    public void Tokenize_RemovesStopwords_AndFoldsPlurals()
    {
        var tokens = DuplicateDetection.Tokenize("The potholes are damaging cars");

        Assert.Contains("pothole", tokens);   // plural folded
        Assert.Contains("car", tokens);        // plural folded
        Assert.DoesNotContain("the", tokens);  // stopword
        Assert.DoesNotContain("are", tokens);  // stopword
        Assert.DoesNotContain("cars", tokens); // folded form only
    }

    [Fact]
    public void TextSimilarity_IdenticalText_IsNearOne()
    {
        var sim = DuplicateDetection.TextSimilarity("pothole on vitosha blvd", "pothole on vitosha blvd");
        Assert.True(sim > 0.9, $"Expected ~1, got {sim}");
    }

    [Fact]
    public void TextSimilarity_DisjointText_IsZero()
    {
        var sim = DuplicateDetection.TextSimilarity("pothole vitosha", "streetlight cherni vrah");
        Assert.Equal(0, sim);
    }

    [Fact]
    public void TextSimilarity_PartialOverlap_IsModerate()
    {
        var sim = DuplicateDetection.TextSimilarity(
            "large pothole damaging cars vitosha",
            "huge pothole cars vitosha boulevard");
        Assert.InRange(sim, 0.3, 0.9);
    }

    [Fact]
    public void DistanceMeters_OneMilliDegreeLatitude_IsAbout111m()
    {
        var d = DuplicateDetection.DistanceMeters(42.000, 23.000, 42.001, 23.000);
        Assert.InRange(d, 100, 125);
    }

    [Theory]
    [InlineData(0.5, false, null, 50)]            // text only
    [InlineData(0.5, true, 100.0, 73)]            // + same category + very close
    [InlineData(0.5, false, 400.0, 58)]           // + moderately close
    [InlineData(0.9, true, 5000.0, 88)]           // far apart applies a penalty
    [InlineData(1.0, true, 100.0, 100)]           // clamped at 100
    public void CombinedScore_AppliesBoostsAndClamps(double textSim, bool sameCategory, double? distance, int expected)
    {
        Assert.Equal(expected, DuplicateDetection.CombinedScore(textSim, sameCategory, distance));
    }

    [Fact]
    public void FindDuplicates_FlagsNearDuplicate_ExcludesUnrelatedAndSelf()
    {
        var target = MakeIssue("T", "Large pothole on Vitosha Blvd damaging cars",
            "Deep pothole near the tram stop.", IssueCategory.Infrastructure, 42.6900, 23.3200);

        var nearDuplicate = MakeIssue("A", "Huge pothole on Vitosha Boulevard, cars getting damaged",
            "The pothole by the tram stop keeps growing.", IssueCategory.Infrastructure, 42.6901, 23.3201);

        var unrelated = MakeIssue("B", "Broken streetlight on Cherni Vrah",
            "The crossing is unlit at night.", IssueCategory.PublicSafety, 42.7000, 23.3500);

        var selfCopy = MakeIssue("T", "Large pothole on Vitosha Blvd damaging cars",
            "Deep pothole near the tram stop.", IssueCategory.Infrastructure, 42.6900, 23.3200);

        var matches = DuplicateDetection.FindDuplicates(target, new[] { nearDuplicate, unrelated, selfCopy });

        var match = Assert.Single(matches);
        Assert.Equal("A", match.IssueId);
        Assert.True(match.SimilarityScore >= 55, $"Expected >=55, got {match.SimilarityScore}");
        Assert.Contains("pothole", match.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindDuplicates_RespectsMaxResults_AndOrdersByScore()
    {
        var target = MakeIssue("T", "Pothole on Vitosha Blvd", "Deep pothole damaging cars.",
            IssueCategory.Infrastructure, 42.69, 23.32);

        var candidates = new[]
        {
            MakeIssue("A", "Pothole on Vitosha Blvd", "Deep pothole damaging cars.", IssueCategory.Infrastructure, 42.69, 23.32),
            MakeIssue("B", "Pothole on Vitosha Boulevard damaging cars", "A deep pothole.", IssueCategory.Infrastructure, 42.69, 23.32),
            MakeIssue("C", "Pothole damaging vehicles near Vitosha", "Deep road pothole.", IssueCategory.Infrastructure, 42.69, 23.32),
            MakeIssue("D", "Vitosha pothole damaging cars", "Deep pothole on the road.", IssueCategory.Infrastructure, 42.69, 23.32),
        };

        var matches = DuplicateDetection.FindDuplicates(target, candidates, maxResults: 2);

        Assert.Equal(2, matches.Count);
        Assert.True(matches[0].SimilarityScore >= matches[1].SimilarityScore, "Results should be ordered by score desc");
    }

    [Fact]
    public void FindDuplicates_NoTargetText_ReturnsEmpty()
    {
        var target = MakeIssue("T", string.Empty, string.Empty, IssueCategory.Other, 42.69, 23.32);
        var candidate = MakeIssue("A", "Pothole on the road", "Deep pothole.", IssueCategory.Infrastructure, 42.69, 23.32);

        Assert.Empty(DuplicateDetection.FindDuplicates(target, new[] { candidate }));
    }

    [Fact]
    public void FindSimilarWithinRadius_FlagsNearbyKeywordMatch_WithSharedTermsAndDistance()
    {
        var draft = MakeIssue("DRAFT", "Large pothole on Vitosha Blvd damaging cars",
            "Deep pothole near the tram stop.", IssueCategory.Infrastructure, 42.6900, 23.3200);
        var nearby = MakeIssue("A", "Huge pothole on Vitosha Boulevard, cars getting damaged",
            "The pothole by the tram stop keeps growing.", IssueCategory.Infrastructure, 42.6901, 23.3201);

        var matches = DuplicateDetection.FindSimilarWithinRadius(draft, new[] { nearby });

        var match = Assert.Single(matches);
        Assert.Equal("A", match.IssueId);
        Assert.Contains("pothole", match.SharedKeywords);
        Assert.NotNull(match.DistanceMeters);
        Assert.InRange(match.DistanceMeters!.Value, 0, 5000);
    }

    [Fact]
    public void FindSimilarWithinRadius_ExcludesMatchesBeyondRadius()
    {
        var draft = MakeIssue("DRAFT", "Large pothole on Vitosha Blvd damaging cars",
            "Deep pothole near the tram stop.", IssueCategory.Infrastructure, 42.6900, 23.3200);
        // ~6.7 km north: identical wording, but outside the 5 km radius.
        var farMatch = MakeIssue("FAR", "Large pothole on Vitosha Blvd damaging cars",
            "Deep pothole near the tram stop.", IssueCategory.Infrastructure, 42.7500, 23.3200);

        Assert.Empty(DuplicateDetection.FindSimilarWithinRadius(draft, new[] { farMatch }, radiusMeters: 5000));
        // Widening the radius brings the same issue back into range.
        Assert.Single(DuplicateDetection.FindSimilarWithinRadius(draft, new[] { farMatch }, radiusMeters: 10000));
    }

    [Fact]
    public void FindSimilarWithinRadius_ExcludesNearbyButUnrelatedKeywords()
    {
        var draft = MakeIssue("DRAFT", "Large pothole on Vitosha Blvd",
            "Deep pothole damaging cars.", IssueCategory.Infrastructure, 42.6900, 23.3200);
        var unrelatedButClose = MakeIssue("U", "Broken streetlight on Cherni Vrah",
            "The crossing is unlit at night.", IssueCategory.PublicSafety, 42.6901, 23.3201);

        Assert.Empty(DuplicateDetection.FindSimilarWithinRadius(draft, new[] { unrelatedButClose }));
    }
}
