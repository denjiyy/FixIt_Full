using FixIt.Data.Repository.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.Locations;
using FixIt.Models.Enums;
using FixIt.Services.Analytics.Models;

namespace FixIt.Services.Analytics;

public interface IHealthReportService
{
    Task<HealthReport> GetCityHealthReportAsync(string cityId);
    Task<HealthReport> GetGlobalHealthReportAsync();
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
        
        // Use CountAsync for simple counts instead of loading all issues
        var totalIssuesCount = await _issueRepo.CountAsync(i => i.CityId == cityId);
        var resolvedIssuesCount = await _issueRepo.CountAsync(i => 
            i.CityId == cityId && i.Status == IssueStatus.Fixed);
        var openIssuesCount = await _issueRepo.CountAsync(i => 
            i.CityId == cityId && i.Status != IssueStatus.Fixed && i.Status != IssueStatus.Rejected);
        
        var report = new HealthReport
        {
            CityId = cityId,
            CityName = city?.Name ?? "Unknown",
            TotalIssues = (int)totalIssuesCount,
            ResolvedIssues = (int)resolvedIssuesCount,
            OpenIssues = (int)openIssuesCount,
        };

        // Calculate resolution rate
        report.ResolutionRate = report.TotalIssues > 0 
            ? (report.ResolvedIssues / (double)report.TotalIssues) * 100 
            : 0;

        // Load only resolved issues for time calculations
        var resolvedIssues = (await _issueRepo.FindAsync(i => 
            i.CityId == cityId && i.Status == IssueStatus.Fixed)).ToList();
        if (resolvedIssues.Count > 0)
        {
            var resolutionTimes = resolvedIssues
                .Select(i => (i.UpdatedAt - i.CreatedAt).TotalHours)
                .ToList();
            report.AverageResolutionTimeHours = resolutionTimes.Average();
        }

        // Response time (time to first update or status change) - load only issues with updates
        var issuesWithUpdates = (await _issueRepo.FindAsync(i => 
            i.CityId == cityId && i.UpdatedAt > i.CreatedAt)).ToList();
        if (issuesWithUpdates.Count > 0)
        {
            var responseTimes = issuesWithUpdates
                .Select(i => (i.UpdatedAt - i.CreatedAt).TotalHours)
                .ToList();
            report.AverageResponseTimeHours = responseTimes.Average();
        }

        // Last 7 days metrics
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var issuesCreatedLast7Days = await _issueRepo.CountAsync(i => 
            i.CityId == cityId && i.CreatedAt >= sevenDaysAgo);
        var issuesResolvedLast7Days = await _issueRepo.CountAsync(i => 
            i.CityId == cityId && i.Status == IssueStatus.Fixed && i.UpdatedAt >= sevenDaysAgo);
        
        report.IssuesCreatedLast7Days = (int)issuesCreatedLast7Days;
        report.IssuesResolvedLast7Days = (int)issuesResolvedLast7Days;

        // Priority distribution using CountAsync
        report.CriticalIssues = (int)await _issueRepo.CountAsync(i => 
            i.CityId == cityId && i.Priority == IssuePriority.Critical);
        report.HighIssues = (int)await _issueRepo.CountAsync(i => 
            i.CityId == cityId && i.Priority == IssuePriority.High);
        report.MediumIssues = (int)await _issueRepo.CountAsync(i => 
            i.CityId == cityId && i.Priority == IssuePriority.Medium);
        report.LowIssues = (int)await _issueRepo.CountAsync(i => 
            i.CityId == cityId && i.Priority == IssuePriority.Low);

        // Load all issues for this city (needed for status breakdown, engagement, top issues)
        var issues = (await _issueRepo.FindAsync(i => i.CityId == cityId)).ToList();

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
            TotalIssues = allIssues.Count,
            ResolvedIssues = allIssues.Count(i => i.Status == IssueStatus.Fixed),
            OpenIssues = allIssues.Count(i => i.Status != IssueStatus.Fixed && i.Status != IssueStatus.Rejected),
        };

        // Calculate resolution rate
        report.ResolutionRate = report.TotalIssues > 0 
            ? (report.ResolvedIssues / (double)report.TotalIssues) * 100 
            : 0;

        // Calculate time metrics
        var resolvedIssues = allIssues.Where(i => i.Status == IssueStatus.Fixed).ToList();
        if (resolvedIssues.Count > 0)
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

    private static double CalculateHealthScore(HealthReport report)
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
