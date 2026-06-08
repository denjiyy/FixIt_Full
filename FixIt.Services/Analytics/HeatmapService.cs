using FixIt.Data.Repository.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.Enums;
using FixIt.Services.Analytics.Models;
using MongoDB.Driver;

namespace FixIt.Services.Analytics;

public interface IHeatmapService
{
    Task<HeatmapData> GetCityHeatmapAsync(string cityId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<List<HeatmapLocationPoint>> GetIssueHotspots(string cityId, int limit = 100);
    Task<Dictionary<string, int>> GetIssuesByTag(string cityId);
}

public class HeatmapService : IHeatmapService
{
    private readonly IRepository<Issue> _issueRepo;
    private readonly IRepository<FixIt.Models.Issues.Tag> _tagRepo;

    public HeatmapService(
        IRepository<Issue> issueRepo,
        IRepository<FixIt.Models.Issues.Tag> tagRepo)
    {
        _issueRepo = issueRepo;
        _tagRepo = tagRepo;
    }

    public async Task<HeatmapData> GetCityHeatmapAsync(string cityId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var fromDateTime = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var toDateTime = toDate ?? DateTime.UtcNow;

        // Use CountAsync for simple counts instead of loading all issues
        var totalIssuesCount = await _issueRepo.CountAsync(i => i.CityId == cityId);
        var resolvedIssuesCount = await _issueRepo.CountAsync(i => 
            i.CityId == cityId && i.Status == IssueStatus.Fixed);
        var openIssuesCount = await _issueRepo.CountAsync(i => 
            i.CityId == cityId && i.Status != IssueStatus.Fixed && i.Status != IssueStatus.Rejected);

        // Load issues only for aggregation (grouping, hotspots, trends)
        var recentIssues = await _issueRepo.FindAsync(i => 
            i.CityId == cityId && 
            i.CreatedAt >= fromDateTime && 
            i.CreatedAt <= toDateTime);
        var allIssues = await _issueRepo.FindAsync(i => i.CityId == cityId);
        
        var recentIssuesList = recentIssues.ToList();
        var allIssuesList = allIssues.ToList();

        var statusDict = GetIssuesByStatusFromList(allIssuesList);
        var priorityDict = GetIssuesByPriorityFromList(allIssuesList);
        var tagsDict = await GetIssuesByTag(cityId);

        var data = new HeatmapData
        {
            CityId = cityId,
            TotalIssues = (int)totalIssuesCount,
            ResolvedIssues = (int)resolvedIssuesCount,
            OpenIssues = (int)openIssuesCount,
            Hotspots = await GetIssueHotspots(cityId, allIssuesList, 100),
            IssuesByStatus = statusDict.Count > 0 ? statusDict : new Dictionary<string, int> { { "No Data", 0 } },
            IssuesByPriority = priorityDict.Count > 0 ? priorityDict : new Dictionary<string, int> { { "No Data", 0 } },
            IssuesByTag = tagsDict.Count > 0 ? tagsDict : new Dictionary<string, int> { { "No Data", 0 } },
            ActivityTrend = GetActivityTrendFromList(recentIssuesList, 30)
        };

        return data;
    }

    public async Task<List<HeatmapLocationPoint>> GetIssueHotspots(string cityId, List<Issue> allIssues, int limit = 100)
    {
        // Filter to unresolved issues with valid location data
        var issues = allIssues
            .Where(i => i.CityId == cityId && 
                   i.Status != IssueStatus.Fixed && 
                   i.Status != IssueStatus.Rejected &&
                   i.Location != null &&
                   i.Location.Coordinates != null)
            .ToList();
        
        if (issues.Count == 0)
            return await Task.FromResult(new List<HeatmapLocationPoint>());
        
        // Group issues by location and create hotspots
        var hotspots = issues
            .GroupBy(i => new { 
                Lat = Math.Round(i.Location.Coordinates.Latitude, 5), 
                Lon = Math.Round(i.Location.Coordinates.Longitude, 5)
            })
            .Select(g => new HeatmapLocationPoint
            {
                Latitude = g.Key.Lat,
                Longitude = g.Key.Lon,
                Intensity = g.Count(),
                Title = $"{g.Count()} issue{(g.Count() > 1 ? "s" : "")}",
                Status = g.First().Status,
                Priority = g.OrderByDescending(i => i.Priority).First().Priority
            })
            .OrderByDescending(h => h.Intensity)
            .Take(limit)
            .ToList();

        return await Task.FromResult(hotspots);
    }

    public async Task<List<HeatmapLocationPoint>> GetIssueHotspots(string cityId, int limit = 100)
    {
        var issues = await _issueRepo.FindAsync(i => i.CityId == cityId);
        return await GetIssueHotspots(cityId, issues.ToList(), limit);
    }



    public async Task<Dictionary<string, int>> GetIssuesByTag(string cityId)
    {
        var issues = await _issueRepo.FindAsync(i => i.CityId == cityId);
        var allTags = await _tagRepo.FindAsync(_ => true);
        
        var tagCounts = new Dictionary<string, int>();
        var tagIdToNameMap = allTags.ToDictionary(t => t.Id, t => t.Name);
        
        // Count issues by individual tags
        foreach (var issue in issues)
        {
            if (issue.TagIds != null && issue.TagIds.Count > 0)
            {
                foreach (var tagId in issue.TagIds)
                {
                    var tagName = tagIdToNameMap.ContainsKey(tagId) ? tagIdToNameMap[tagId] : $"Tag_{tagId}";
                    if (tagCounts.ContainsKey(tagName))
                        tagCounts[tagName]++;
                    else
                        tagCounts[tagName] = 1;
                }
            }
        }
        
        // If no tagged issues, return count of untagged issues
        if (tagCounts.Count == 0)
        {
            var untaggedCount = issues.Count(i => i.TagIds == null || i.TagIds.Count == 0);
            if (untaggedCount > 0)
            {
                tagCounts["Untagged"] = untaggedCount;
            }
        }
        
        return tagCounts;
    }



    private Dictionary<string, int> GetIssuesByStatusFromList(List<Issue> issues)
    {
        return issues
            .GroupBy(i => i.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private Dictionary<string, int> GetIssuesByPriorityFromList(List<Issue> issues)
    {
        return issues
            .GroupBy(i => i.Priority.ToString())
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private List<ActivityTrendData> GetActivityTrendFromList(List<Issue> issues, int days)
    {
        var trend = new List<ActivityTrendData>();
        for (int i = 0; i < days; i++)
        {
            var date = DateTime.UtcNow.AddDays(-days + i).Date;

            var data = new ActivityTrendData
            {
                Date = date,
                NewIssues = issues.Count(x => x.CreatedAt.Date == date),
                ResolvedIssues = issues.Count(x => x.Status == IssueStatus.Fixed && x.UpdatedAt.Date == date),
                UpdatedIssues = issues.Count(x => x.UpdatedAt.Date == date && x.CreatedAt.Date != date)
            };

            trend.Add(data);
        }

        return trend;
    }
}
