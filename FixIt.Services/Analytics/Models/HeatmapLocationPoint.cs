using FixIt.Models.Enums;

namespace FixIt.Services.Analytics.Models;

/// <summary>
/// Geographic point representing a hotspot of issues
/// </summary>
public class HeatmapLocationPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Intensity { get; set; }
    public string Title { get; set; } = string.Empty;
    public IssueStatus Status { get; set; }
    public IssuePriority Priority { get; set; }
}
