using FixIt.Models.Enums;

namespace FixIt.Services.Analytics.Models;

/// <summary>
/// Comprehensive health report for a city or global view
/// </summary>
public class HealthReport
{
    public string CityId { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    
    // Overall metrics
    public int TotalIssues { get; set; }
    public int ResolvedIssues { get; set; }
    public int OpenIssues { get; set; }
    public double ResolutionRate { get; set; }
    
    // Time metrics
    public double AverageResolutionTimeHours { get; set; }
    public double AverageResponseTimeHours { get; set; }
    public int IssuesCreatedLast7Days { get; set; }
    public int IssuesResolvedLast7Days { get; set; }
    
    // Priority distribution
    public int CriticalIssues { get; set; }
    public int HighIssues { get; set; }
    public int MediumIssues { get; set; }
    public int LowIssues { get; set; }
    
    // Status breakdown
    public Dictionary<string, int> IssuesByStatus { get; set; } = new();
    
    // Engagement metrics
    public int TotalComments { get; set; }
    public int TotalUpvotes { get; set; }
    public int AverageUpvotesPerIssue { get; set; }
    public int ActiveUsersLast30Days { get; set; }
    
    // Community health score (0-100)
    public double HealthScore { get; set; }
    
    // Top issues
    public List<TopIssueInfo> TopIssues { get; set; } = new();
}
