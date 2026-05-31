using FixIt.Models.AI;
using FixIt.Models.Enums;
using FixIt.Services.AI;
using Xunit;

namespace FixIt.Tests.Services.AI;

/// <summary>
/// Calibration tests for the shared no-API-key classifier. These lock in the
/// behaviour both fallback engines (issue analysis + civic draft suggestions)
/// rely on, so they can never silently drift apart again.
/// </summary>
public class IssueHeuristicsTests
{
    [Theory]
    [InlineData("Large pothole on the road by the tram stop", IssueCategory.Infrastructure)]
    [InlineData("Broken streetlight leaves the crossing dark at night", IssueCategory.PublicSafety)]
    [InlineData("Street lamp is out on the corner", IssueCategory.PublicSafety)]
    [InlineData("Urgent gas leak near homes", IssueCategory.Utilities)]
    [InlineData("No power and a downed power line on Main St", IssueCategory.Utilities)]
    [InlineData("Graffiti sprayed across the wall", IssueCategory.Sanitation)]
    [InlineData("Overflowing trash bins and litter everywhere", IssueCategory.Sanitation)]
    [InlineData("Illegal dumping in the ravine", IssueCategory.EnvironmentalHealth)]
    [InlineData("Heavy traffic congestion at the intersection", IssueCategory.Transportation)]
    [InlineData("Broken swing in the playground", IssueCategory.Parks)]
    [InlineData("Rats infestation near the market", IssueCategory.PublicHealth)]
    [InlineData("xyzzy plugh", IssueCategory.Other)]
    public void Classify_MapsCategoryFromKeywords(string title, IssueCategory expected)
    {
        var result = IssueHeuristics.Classify(title, string.Empty);
        Assert.Equal(expected, result.Category);
    }

    [Fact]
    public void Classify_BrokenStreetlight_IsPublicSafety_NotOther()
    {
        // The old fallback returned Other/Medium for streetlights — the gap this fix closes.
        var result = IssueHeuristics.Classify(
            "Broken streetlight",
            "The pedestrian crossing is unlit and feels unsafe after dark.");

        Assert.Equal(IssueCategory.PublicSafety, result.Category);
        Assert.True(result.Severity >= 6, $"Expected elevated severity, got {result.Severity}");
        Assert.Equal(IssuePriority.High, result.Priority);
        Assert.Contains("safety-concern", result.Tags);
    }

    [Fact]
    public void Classify_UrgentGasLeak_IsCritical()
    {
        var result = IssueHeuristics.Classify("Urgent gas leak", "Strong smell of gas, please respond immediately.");

        Assert.Equal(IssueCategory.Utilities, result.Category);
        Assert.Equal(IssuePriority.Critical, result.Priority);
        Assert.Contains("urgent", result.Tags);
    }

    [Fact]
    public void Classify_MinorCosmeticIssue_IsLowPriority()
    {
        var result = IssueHeuristics.Classify("Minor cosmetic blemish", "A small, slight scuff — non-urgent.");
        Assert.Equal(IssuePriority.Low, result.Priority);
    }

    [Fact]
    public void Classify_PriorityIsAlwaysDerivedFromSeverity()
    {
        // The invariant that keeps the two engines consistent: priority is a pure
        // function of severity, never set independently.
        foreach (var sample in new[]
        {
            "Urgent gas leak near a school",
            "Broken streetlight on a dark street",
            "Pothole on the road",
            "Minor faded paint",
            "xyzzy plugh"
        })
        {
            var result = IssueHeuristics.Classify(sample, string.Empty);
            Assert.Equal(IssueHeuristics.PriorityFromSeverity(result.Severity), result.Priority);
        }
    }

    [Theory]
    [InlineData(10, IssuePriority.Critical)]
    [InlineData(9, IssuePriority.Critical)]
    [InlineData(8, IssuePriority.High)]
    [InlineData(7, IssuePriority.High)]
    [InlineData(6, IssuePriority.Medium)]
    [InlineData(4, IssuePriority.Medium)]
    [InlineData(3, IssuePriority.Low)]
    [InlineData(1, IssuePriority.Low)]
    public void PriorityFromSeverity_MapsBands(int severity, IssuePriority expected)
    {
        Assert.Equal(expected, IssueHeuristics.PriorityFromSeverity(severity));
    }

    [Theory]
    [InlineData(IssueCategory.PublicSafety, "Public Safety Department")]
    [InlineData(IssueCategory.Utilities, "Utilities Department")]
    [InlineData(IssueCategory.Parks, "Parks and Recreation")]
    [InlineData(IssueCategory.Other, "General Services")]
    public void DepartmentFor_MapsCategory(IssueCategory category, string expected)
    {
        Assert.Equal(expected, IssueHeuristics.DepartmentFor(category));
    }

    [Fact]
    public void Classify_EmptyInput_IsOther_WithReviewTag_AndLowConfidence()
    {
        var result = IssueHeuristics.Classify(string.Empty, string.Empty);

        Assert.Equal(IssueCategory.Other, result.Category);
        Assert.Contains("review-needed", result.Tags);
        Assert.True(result.Confidence <= 40, $"Expected low confidence, got {result.Confidence}");
    }

    [Fact]
    public void Classify_StrongMatch_HasHigherConfidenceThanOther()
    {
        var strong = IssueHeuristics.Classify("Traffic congestion and illegal parking at the intersection", string.Empty);
        var vague = IssueHeuristics.Classify("xyzzy plugh", string.Empty);

        Assert.True(strong.Confidence > vague.Confidence,
            $"Strong match ({strong.Confidence}) should beat vague ({vague.Confidence})");
        Assert.True(strong.Confidence >= 60, $"Expected confident multi-keyword match, got {strong.Confidence}");
    }

    [Fact]
    public void Classify_IsDeterministic()
    {
        var a = IssueHeuristics.Classify("Pothole damaging cars on the road", "Deep and worsening.");
        var b = IssueHeuristics.Classify("Pothole damaging cars on the road", "Deep and worsening.");

        Assert.Equal(a.Category, b.Category);
        Assert.Equal(a.Severity, b.Severity);
        Assert.Equal(a.Priority, b.Priority);
        Assert.Equal(a.Confidence, b.Confidence);
    }
}
