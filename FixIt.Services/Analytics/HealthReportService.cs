using FixIt.Data.Repository.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.Locations;
using FixIt.Models.Enums;
using FixIt.Models.Users;

namespace FixIt.Services.Analytics;

public interface IHealthReportService
{
    Task<HealthReport> GetCityHealthReportAsync(string cityId);
    Task<HealthReport> GetGlobalHealthReportAsync();
}

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

public class TopIssueInfo
{
    public string IssueId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Upvotes { get; set; }
    public int Comments { get; set; }
    public IssueStatus Status { get; set; }
    public IssuePriority Priority { get; set; }
}

public class HealthReportService : IHealthReportService
{
    private readonly IRepository<Issue> _issueRepo;
    private readonly IRepository<City> _cityRepo;

    public HealthReportService(
        IRepository<Issue> issueRepo,
        IRepository<City> cityRepo)
    {
        _issueRepo = issueRepo;
        _cityRepo = cityRepo;
    }

    public async Task<HealthReport> GetCityHealthReportAsync(string cityId)
    {
        var city = await _cityRepo.GetByIdAsync(cityId);
        var issues = (await _issueRepo.FindAsync(i => i.CityId == cityId)).ToList();

        var report = new HealthReport
        {
            CityId = cityId,
            CityName = city?.Name ?? "Unknown",
            TotalIssues = issues.Count(),
            ResolvedIssues = issues.Count(i => i.Status == IssueStatus.Fixed),
            OpenIssues = issues.Count(i => i.Status != IssueStatus.Fixed && i.Status != IssueStatus.Rejected),
        };

        // Calculate resolution rate
        report.ResolutionRate = report.TotalIssues > 0 
            ? (report.ResolvedIssues / (double)report.TotalIssues) * 100 
            : 0;

        // Calculate time metrics
        var resolvedIssues = issues.Where(i => i.Status == IssueStatus.Fixed).ToList();
        if (resolvedIssues.Any())
        {
            var resolutionTimes = resolvedIssues
                .Select(i => (i.UpdatedAt - i.CreatedAt).TotalHours)
                .ToList();
            report.AverageResolutionTimeHours = resolutionTimes.Average();
        }

        // Response time (time to first update or status change)
        var issuesWithUpdates = issues.Where(i => i.UpdatedAt > i.CreatedAt).ToList();
        if (issuesWithUpdates.Any())
        {
            var responseTimes = issuesWithUpdates
                .Select(i => (i.UpdatedAt - i.CreatedAt).TotalHours)
                .ToList();
            report.AverageResponseTimeHours = responseTimes.Average();
        }

        // Last 7 days metrics
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        report.IssuesCreatedLast7Days = issues.Count(i => i.CreatedAt >= sevenDaysAgo);
        report.IssuesResolvedLast7Days = issues.Count(i => 
            i.Status == IssueStatus.Fixed && 
            i.UpdatedAt >= sevenDaysAgo);

        // Priority distribution
        report.CriticalIssues = issues.Count(i => i.Priority == IssuePriority.Critical);
        report.HighIssues = issues.Count(i => i.Priority == IssuePriority.High);
        report.MediumIssues = issues.Count(i => i.Priority == IssuePriority.Medium);
        report.LowIssues = issues.Count(i => i.Priority == IssuePriority.Low);

        // Status breakdown
        report.IssuesByStatus = issues
            .GroupBy(i => i.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // Engagement metrics
        report.TotalComments = issues.Sum(i => i.CommentCount);
        report.TotalUpvotes = issues.Sum(i => i.Upvotes);
        report.AverageUpvotesPerIssue = report.TotalIssues > 0 
            ? report.TotalUpvotes / report.TotalIssues 
            : 0;

        // Active users (users who created or updated issues in last 30 days)
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var activeUserIds = new HashSet<string>();
        foreach (var issue in issues.Where(i => i.CreatedAt >= thirtyDaysAgo || i.UpdatedAt >= thirtyDaysAgo))
        {
            activeUserIds.Add(issue.Reporter.Id);
        }
        report.ActiveUsersLast30Days = activeUserIds.Count;

        // Top issues
        report.TopIssues = issues
            .OrderByDescending(i => i.Upvotes)
            .Take(5)
            .Select(i => new TopIssueInfo
            {
                IssueId = i.Id,
                Title = i.Title,
                Upvotes = i.Upvotes,
                Comments = i.CommentCount,
                Status = i.Status,
                Priority = i.Priority
            })
            .ToList();

        // Calculate health score (0-100)
        report.HealthScore = CalculateHealthScore(report);

        return report;
    }

    public async Task<HealthReport> GetGlobalHealthReportAsync()
    {
        var allIssues = (await _issueRepo.FindAsync(_ => true)).ToList();
        var allCities = await _cityRepo.FindAsync(_ => true);

        var report = new HealthReport
        {
            CityId = "global",
            CityName = "Global",
            TotalIssues = allIssues.Count(),
            ResolvedIssues = allIssues.Count(i => i.Status == IssueStatus.Fixed),
            OpenIssues = allIssues.Count(i => i.Status != IssueStatus.Fixed && i.Status != IssueStatus.Rejected),
        };

        // Calculate resolution rate
        report.ResolutionRate = report.TotalIssues > 0 
            ? (report.ResolvedIssues / (double)report.TotalIssues) * 100 
            : 0;

        // Calculate time metrics
        var resolvedIssues = allIssues.Where(i => i.Status == IssueStatus.Fixed).ToList();
        if (resolvedIssues.Any())
        {
            var resolutionTimes = resolvedIssues
                .Select(i => (i.UpdatedAt - i.CreatedAt).TotalHours)
                .ToList();
            report.AverageResolutionTimeHours = resolutionTimes.Average();
        }

        // Priority distribution
        report.CriticalIssues = allIssues.Count(i => i.Priority == IssuePriority.Critical);
        report.HighIssues = allIssues.Count(i => i.Priority == IssuePriority.High);
        report.MediumIssues = allIssues.Count(i => i.Priority == IssuePriority.Medium);
        report.LowIssues = allIssues.Count(i => i.Priority == IssuePriority.Low);

        // Status breakdown
        report.IssuesByStatus = allIssues
            .GroupBy(i => i.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // Engagement metrics
        report.TotalComments = allIssues.Sum(i => i.CommentCount);
        report.TotalUpvotes = allIssues.Sum(i => i.Upvotes);
        report.AverageUpvotesPerIssue = report.TotalIssues > 0 
            ? report.TotalUpvotes / report.TotalIssues 
            : 0;

        // Active users
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var activeUserIds = new HashSet<string>();
        foreach (var issue in allIssues.Where(i => i.CreatedAt >= thirtyDaysAgo || i.UpdatedAt >= thirtyDaysAgo))
        {
            activeUserIds.Add(issue.Reporter.Id);
        }
        report.ActiveUsersLast30Days = activeUserIds.Count;

        // Top issues
        report.TopIssues = allIssues
            .OrderByDescending(i => i.Upvotes)
            .Take(5)
            .Select(i => new TopIssueInfo
            {
                IssueId = i.Id,
                Title = i.Title,
                Upvotes = i.Upvotes,
                Comments = i.CommentCount,
                Status = i.Status,
                Priority = i.Priority
            })
            .ToList();

        // Calculate health score
        report.HealthScore = CalculateHealthScore(report);

        return report;
    }

    private double CalculateHealthScore(HealthReport report)
    {
        // If there are no open issues, the city is in perfect health
        if (report.OpenIssues == 0)
        {
            return 100.0;
        }

        // Otherwise, deduct 5 points per open issue
        double score = 100.0 - (report.OpenIssues * 5.0);

        // Ensure score never goes below 0
        return Math.Max(0, score);
    }
}
