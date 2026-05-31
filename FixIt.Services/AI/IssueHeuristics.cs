using FixIt.Models.AI;
using FixIt.Models.Enums;

namespace FixIt.Services.AI;

/// <summary>
/// Outcome of the rule-based (no-API-key) classification of a civic issue.
/// </summary>
public sealed record IssueHeuristicResult
{
    public IssueCategory Category { get; init; } = IssueCategory.Other;

    /// <summary>Estimated severity, 1–10.</summary>
    public int Severity { get; init; } = 5;

    /// <summary>Priority derived from <see cref="Severity"/> (and urgency cues).</summary>
    public IssuePriority Priority { get; init; } = IssuePriority.Medium;

    /// <summary>Confidence in the classification, 0–100. Scales with match strength.</summary>
    public int Confidence { get; init; } = 45;

    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public string Department { get; init; } = "General Services";
}

/// <summary>
/// Single source of truth for keyword-based classification of a civic issue from
/// its title + description. Both no-API-key fallbacks — the issue analysis service
/// and the civic draft-suggestion service — call this, so they can never disagree
/// on the same input. When OpenAI is configured the model output takes precedence;
/// this only runs when the key is absent or the call fails.
/// </summary>
public static class IssueHeuristics
{
    // Category signals. Ordered most-specific / safety-first first so that, on an
    // equal number of keyword hits, the earlier category wins the tie.
    private static readonly IReadOnlyList<(IssueCategory Category, string[] Keywords)> CategorySignals =
        new[]
        {
            (IssueCategory.PublicSafety, new[]
            {
                "streetlight", "street light", "street lamp", "lamppost", "lamp post", "lighting",
                "light is out", "light out", "lights out", "no lighting", "unlit", "dark", "poorly lit",
                "broken glass", "open manhole", "missing manhole", "manhole cover", "exposed wire", "downed wire",
                "crime", "theft", "stolen", "robbery", "burglary", "assault", "violence", "weapon", "gunshot",
                "vandalism", "unsafe", "danger", "dangerous", "hazard", "hazardous", "accident", "collision",
                "suspicious", "harassment", "trespass"
            }),
            (IssueCategory.Utilities, new[]
            {
                "water main", "water leak", "burst pipe", "no water", "low pressure", "pipe", "hydrant",
                "gas leak", "smell of gas", "gas smell", "electric", "electricity", "power outage", "power line",
                "no power", "blackout", "sewage", "sewer", "drain", "manhole", "cable", "telecom", "utility"
            }),
            (IssueCategory.EnvironmentalHealth, new[]
            {
                "pollution", "polluted", "contamination", "contaminated", "odor", "odour", "stench", "bad smell",
                "flood", "flooding", "standing water", "chemical spill", "oil spill", "smoke", "air quality",
                "illegal dumping", "dumping", "runoff"
            }),
            (IssueCategory.PublicHealth, new[]
            {
                "disease", "illness", "outbreak", "rodent", "rats", "rat ", "mosquito", "infestation", "pest",
                "mold", "mould", "biohazard", "needle", "syringe", "health hazard", "unsanitary"
            }),
            (IssueCategory.Infrastructure, new[]
            {
                "pothole", "road surface", "roadway", "pavement", "asphalt", "sidewalk", "footpath", "bridge",
                "curb", "kerb", "guardrail", "retaining wall", "structural", "sinkhole", "crack in the road",
                "crumbling", "broken pavement"
            }),
            (IssueCategory.Transportation, new[]
            {
                "traffic", "parking", "bus stop", "bus lane", "bus shelter", "transit", "intersection",
                "congestion", "traffic light", "traffic signal", "stop sign", "crosswalk", "pedestrian crossing",
                "bike lane", "cycle lane", "speeding", "road sign", "metro", "tram", "railway crossing"
            }),
            (IssueCategory.Sanitation, new[]
            {
                "trash", "garbage", "rubbish", "litter", "recycling", "overflowing bin", "dumpster", "graffiti",
                "debris", "fly-tipping", "waste", "uncollected"
            }),
            (IssueCategory.Parks, new[]
            {
                "playground", "green space", "park bench", "community garden", "fallen tree", "tree", "trail",
                "recreation", "sports field", "fountain", "park "
            }),
        };

    private static readonly string[] CriticalSignals =
    {
        "emergency", "urgent", "immediate", "immediately", "critical", "life-threatening", "life threatening",
        "gas leak", "exposed wire", "downed wire", "downed power line", "live wire", "collapse", "collapsed",
        "fire", "explosion", "trapped", "injury", "injured", "bleeding", "child", "children", "school zone"
    };

    private static readonly string[] HighSignals =
    {
        "dangerous", "danger", "hazard", "hazardous", "unsafe", "accident", "collision", "deep", "blocked",
        "blocking", "leak", "leaking", "sharp", "broken", "damaged", "sinkhole", "flooding", "sewage",
        "exposed", "worsening", "spreading"
    };

    private static readonly string[] LowSignals =
    {
        "minor", "small", "slight", "slightly", "cosmetic", "faded", "tiny", "little", "low priority"
    };

    // Categories that are inherently serious get a severity floor even without an
    // explicit high/critical keyword (e.g. a plain "broken streetlight").
    private static readonly HashSet<IssueCategory> ElevatedCategories = new()
    {
        IssueCategory.PublicSafety,
        IssueCategory.Utilities,
        IssueCategory.PublicHealth,
        IssueCategory.EnvironmentalHealth,
    };

    /// <summary>
    /// Classifies an issue draft into a category, severity, priority, confidence,
    /// keywords, tags and the responsible department.
    /// </summary>
    public static IssueHeuristicResult Classify(string? title, string? description)
    {
        var text = $"{title} {description}".ToLowerInvariant();

        // Neutralize negated urgency so "non-urgent" / "not urgent" don't match the
        // "urgent" critical cue; map them to a low-priority signal instead.
        text = text
            .Replace("non-urgent", " minor ", StringComparison.Ordinal)
            .Replace("non urgent", " minor ", StringComparison.Ordinal)
            .Replace("not urgent", " minor ", StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(text))
        {
            return new IssueHeuristicResult
            {
                Confidence = 35,
                Tags = new[] { "report", "review-needed" },
                Department = DepartmentFor(IssueCategory.Other),
            };
        }

        // ---- Category: pick the best-scoring set; ties go to the earlier (safety-first) entry. ----
        var category = IssueCategory.Other;
        var matchedKeywords = new List<string>();
        var bestHits = 0;

        foreach (var (candidate, keywords) in CategorySignals)
        {
            var hits = keywords.Where(k => text.Contains(k, StringComparison.Ordinal)).ToList();
            if (hits.Count > bestHits)
            {
                bestHits = hits.Count;
                category = candidate;
                matchedKeywords = hits;
            }
        }

        // ---- Severity (1–10): low → high → critical, later cues override earlier. ----
        var severity = 5;
        var matchedLow = LowSignals.Where(s => text.Contains(s, StringComparison.Ordinal)).ToList();
        var matchedHigh = HighSignals.Where(s => text.Contains(s, StringComparison.Ordinal)).ToList();
        var matchedCritical = CriticalSignals.Where(s => text.Contains(s, StringComparison.Ordinal)).ToList();

        if (matchedLow.Count > 0) { severity = 3; }
        if (matchedHigh.Count > 0) { severity = 7; }
        if (matchedCritical.Count > 0) { severity = 9; }

        // Inherently serious categories never read as trivial.
        if (ElevatedCategories.Contains(category))
        {
            severity = Math.Max(severity, 6);
        }

        severity = Math.Clamp(severity, 1, 10);

        // ---- Priority derived from severity (single mapping shared everywhere). ----
        var priority = PriorityFromSeverity(severity);

        // ---- Confidence scales with how strongly the text matched a category. ----
        var confidence = category == IssueCategory.Other
            ? 38
            : Math.Clamp(45 + bestHits * 12 + (matchedHigh.Count + matchedCritical.Count > 0 ? 5 : 0), 0, 92);

        // ---- Keywords + tags. ----
        var resultKeywords = matchedKeywords
            .Concat(matchedCritical)
            .Concat(matchedHigh)
            .Select(k => k.Trim())
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        return new IssueHeuristicResult
        {
            Category = category,
            Severity = severity,
            Priority = priority,
            Confidence = confidence,
            Keywords = resultKeywords,
            Tags = BuildTags(category, severity, matchedCritical.Count > 0),
            Department = DepartmentFor(category),
        };
    }

    /// <summary>Shared severity (1–10) → priority mapping.</summary>
    public static IssuePriority PriorityFromSeverity(int severity) => severity switch
    {
        >= 9 => IssuePriority.Critical,
        >= 7 => IssuePriority.High,
        >= 4 => IssuePriority.Medium,
        _ => IssuePriority.Low,
    };

    /// <summary>Maps a category to the responsible municipal department.</summary>
    public static string DepartmentFor(IssueCategory category) => category switch
    {
        IssueCategory.Infrastructure => "Public Works Department",
        IssueCategory.PublicSafety => "Public Safety Department",
        IssueCategory.EnvironmentalHealth => "Environmental Health Department",
        IssueCategory.Parks => "Parks and Recreation",
        IssueCategory.Transportation => "Transportation Department",
        IssueCategory.Utilities => "Utilities Department",
        IssueCategory.Sanitation => "Sanitation Department",
        IssueCategory.PublicHealth => "Public Health Department",
        _ => "General Services",
    };

    private static List<string> BuildTags(IssueCategory category, int severity, bool critical)
    {
        var tags = new List<string>();

        var categoryTag = category switch
        {
            IssueCategory.Infrastructure => "infrastructure",
            IssueCategory.PublicSafety => "safety-concern",
            IssueCategory.EnvironmentalHealth => "environment",
            IssueCategory.Parks => "parks",
            IssueCategory.Transportation => "transportation",
            IssueCategory.Utilities => "utilities",
            IssueCategory.Sanitation => "sanitation",
            IssueCategory.PublicHealth => "public-health",
            _ => null,
        };
        if (categoryTag != null) { tags.Add(categoryTag); }

        if (critical || severity >= 9) { tags.Add("urgent"); }
        if (category == IssueCategory.PublicSafety && !tags.Contains("safety-concern")) { tags.Add("safety-concern"); }
        if (tags.Count == 0) { tags.Add("review-needed"); }

        return tags;
    }
}
