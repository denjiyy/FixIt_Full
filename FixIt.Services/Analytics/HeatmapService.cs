using FixIt.Data.Repository.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.Locations;
using FixIt.Models.Enums;
using MongoDB.Driver;

namespace FixIt.Services.Analytics;

public interface IHeatmapService
{
    Task<HeatmapData> GetCityHeatmapAsync(string cityId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<List<HeatmapLocationPoint>> GetIssueHotspots(string cityId, int limit = 100);
    Task<Dictionary<string, int>> GetIssuesByPriority(string cityId);
    Task<Dictionary<string, int>> GetIssuesByStatus(string cityId);
    Task<Dictionary<string, int>> GetIssuesByTag(string cityId);
    Task<List<ActivityTrendData>> GetActivityTrend(string cityId, int days = 30);
}

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

public class HeatmapLocationPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Intensity { get; set; }
    public string Title { get; set; } = string.Empty;
    public IssueStatus Status { get; set; }
    public IssuePriority Priority { get; set; }
}

public class ActivityTrendData
{
    public DateTime Date { get; set; }
    public int NewIssues { get; set; }
    public int ResolvedIssues { get; set; }
    public int UpdatedIssues { get; set; }
}

public class HeatmapService : IHeatmapService
{
    private readonly IRepository<Issue> _issueRepo;
    private readonly IRepository<City> _cityRepo;
    private readonly IRepository<FixIt.Models.Issues.Tag> _tagRepo;

    public HeatmapService(
        IRepository<Issue> issueRepo,
        IRepository<City> cityRepo,
        IRepository<FixIt.Models.Issues.Tag> tagRepo)
    {
        _issueRepo = issueRepo;
        _cityRepo = cityRepo;
        _tagRepo = tagRepo;
    }

    public async Task<HeatmapData> GetCityHeatmapAsync(string cityId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var fromDateTime = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var toDateTime = toDate ?? DateTime.UtcNow;

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
            TotalIssues = allIssuesList.Count(),
            ResolvedIssues = allIssuesList.Count(i => i.Status == IssueStatus.Fixed),
            OpenIssues = allIssuesList.Count(i => i.Status != IssueStatus.Fixed && i.Status != IssueStatus.Rejected),
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
            return new List<HeatmapLocationPoint>();
        
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

        return hotspots;
    }

    public async Task<List<HeatmapLocationPoint>> GetIssueHotspots(string cityId, int limit = 100)
    {
        var issues = await _issueRepo.FindAsync(i => i.CityId == cityId);
        return await GetIssueHotspots(cityId, issues.ToList(), limit);
    }

    public async Task<Dictionary<string, int>> GetIssuesByPriority(string cityId)
    {
        var issues = await _issueRepo.FindAsync(i => i.CityId == cityId);
        return issues
            .GroupBy(i => i.Priority.ToString())
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetIssuesByStatus(string cityId)
    {
        var issues = await _issueRepo.FindAsync(i => i.CityId == cityId);
        return GetIssuesByStatusFromList(issues.ToList());
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

    public async Task<List<ActivityTrendData>> GetActivityTrend(string cityId, int days = 30)
    {
        var fromDate = DateTime.UtcNow.AddDays(-days);
        var issues = await _issueRepo.FindAsync(i => 
            i.CityId == cityId && 
            i.CreatedAt >= fromDate);

        return GetActivityTrendFromList(issues.ToList(), days);
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
