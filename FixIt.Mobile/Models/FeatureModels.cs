namespace FixIt.Mobile.Models;

public sealed class LeaderboardResult
{
    public List<LeaderboardEntry> Entries { get; set; } = [];
}

public sealed class LeaderboardEntry
{
    public int Rank { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public int Points { get; set; }
    public int TrustLevel { get; set; }

    public string Initials => BuildInitials(UserDisplayName);

    public string MedalPrefix => Rank switch
    {
        1 => "🥇",
        2 => "🥈",
        3 => "🥉",
        _ => Rank.ToString()
    };

    private static string BuildInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "F";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 1
            ? parts[0][0].ToString().ToUpperInvariant()
            : string.Concat(parts[0][0], parts[1][0]).ToUpperInvariant();
    }
}

public sealed class CityHealthReport
{
    public double HealthScore { get; set; }
    public int TotalIssues { get; set; }
    public int ResolvedIssues { get; set; }
    public int OpenIssues { get; set; }
    public double ResolutionRate { get; set; }
    public int CriticalIssues { get; set; }
    public int HighIssues { get; set; }
    public int MediumIssues { get; set; }
    public int LowIssues { get; set; }
    public int TotalUpvotes { get; set; }
    public int TotalComments { get; set; }
}

public sealed class IssueAnalysis
{
    public string Category { get; set; } = string.Empty;
    public int ConfidenceScore { get; set; }
    public int EstimatedSeverity { get; set; }
    public List<string> Keywords { get; set; } = [];
    public List<string> SuggestedTags { get; set; } = [];
    public string Reasoning { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
}

public sealed class IssueFilterResult
{
    public string? SearchQuery { get; set; }
    public int? Status { get; set; }
    public int? Priority { get; set; }
    public string? Category { get; set; }
}

public sealed class PublicUserProfile
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int TrustLevel { get; set; }
    public int ReputationPoints { get; set; }
    public int IssuesReported { get; set; }
    public int IssuesResolved { get; set; }
    public int CommentsPosted { get; set; }

    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                return "F";
            }

            var parts = DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length == 1
                ? parts[0][0].ToString().ToUpperInvariant()
                : string.Concat(parts[0][0], parts[1][0]).ToUpperInvariant();
        }
    }
}

public sealed class DraftSuggestion
{
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int ConfidenceScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

public sealed class HazardClusterInsight
{
    public string Summary { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public int Severity { get; set; }
}

public sealed class AlertPreferences
{
    public bool CrimeAlertsEnabled { get; set; } = true;
    public bool AccidentAlertsEnabled { get; set; } = true;
    public bool InfrastructureAlertsEnabled { get; set; } = true;
    public double RadiusKm { get; set; } = 5;
    public string SeverityThreshold { get; set; } = "Medium";
}

public sealed class ReverseGeocodeResult
{
    public string Address { get; set; } = string.Empty;
    public string? CityName { get; set; }
    public string? CityId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public sealed class Tag
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

public sealed class IssueStatusEvent
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? Comment { get; set; }
    public string? ChangedByUserId { get; set; }
}
