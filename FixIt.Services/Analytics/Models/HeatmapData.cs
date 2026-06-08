namespace FixIt.Services.Analytics.Models;

/// <summary>
/// Complete heatmap data for a city including hotspots and statistics
/// </summary>
public class HeatmapData
{
    public string CityId { get; set; } = string.Empty;
    public List<HeatmapLocationPoint> Hotspots { get; set; } = new();
    public Dictionary<string, int> IssuesByStatus { get; set; } = new();
    public Dictionary<string, int> IssuesByPriority { get; set; } = new();
    public Dictionary<string, int> IssuesByTag { get; set; } = new();
    public List<ActivityTrendData> ActivityTrend { get; set; } = new();
    public int TotalIssues { get; set; }
    public int ResolvedIssues { get; set; }
    public int OpenIssues { get; set; }
}
