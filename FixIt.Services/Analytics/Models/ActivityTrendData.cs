namespace FixIt.Services.Analytics.Models;

/// <summary>
/// Activity trend data for a specific date
/// </summary>
public class ActivityTrendData
{
    public DateTime Date { get; set; }
    public int NewIssues { get; set; }
    public int ResolvedIssues { get; set; }
    public int UpdatedIssues { get; set; }
}
