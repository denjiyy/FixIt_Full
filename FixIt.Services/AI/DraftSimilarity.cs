using FixIt.Models.AI;

namespace FixIt.Services.AI;

/// <summary>
/// Input for the pre-submit "this may already be reported" check. Built from an
/// in-progress report draft (title, description, pinned location, city).
/// </summary>
public sealed class DraftSimilarityQuery
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? CityId { get; set; }
    public IssueCategory? Category { get; set; }

    /// <summary>Radius for "nearby" duplicates. Defaults to 5 km per product spec.</summary>
    public double RadiusKm { get; set; } = 5;
}

/// <summary>
/// One existing issue that resembles the draft, with everything the UI needs to
/// render a non-blocking reminder: a link target (issue id), the matched keywords,
/// and how far away it is.
/// </summary>
public sealed class SimilarIssueResult
{
    public string IssueId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int SimilarityScore { get; set; }
    public double DistanceKm { get; set; }
    public List<string> MatchedKeywords { get; set; } = new();
    public string Status { get; set; } = string.Empty;
}
