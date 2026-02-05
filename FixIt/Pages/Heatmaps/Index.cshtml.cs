using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Services.Analytics;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Locations;
using System.Text.Json;

namespace FixIt.Pages.Heatmaps;

public class HeatmapsModel : PageModel
{
    private readonly IHeatmapService _heatmapService;
    private readonly IRepository<City> _cityRepo;

    public string CityName { get; set; } = string.Empty;
    public List<IssueMarker> IssueMarkers { get; set; } = new();
    public string MarkersJson { get; set; } = "[]";
    
    public HeatmapsModel(IHeatmapService heatmapService, IRepository<City> cityRepo)
    {
        _heatmapService = heatmapService;
        _cityRepo = cityRepo;
    }

    public async Task OnGetAsync(string cityId)
    {
        var city = await _cityRepo.GetByIdAsync(cityId);
        if (city == null)
        {
            RedirectToPage("/Cities");
            return;
        }

        CityName = city.Name;

        // Get heatmap data
        var heatmapData = await _heatmapService.GetCityHeatmapAsync(cityId);

        // Convert hotspots to markers with issue IDs
        var issueRepo = HttpContext.RequestServices.GetRequiredService<IRepository<FixIt.Models.Issues.Issue>>();
        var allIssues = (await issueRepo.FindAsync(i => i.CityId == cityId)).ToList();

        IssueMarkers = new List<IssueMarker>();
        foreach (var issue in allIssues)
        {
            if (issue.Location?.Coordinates != null)
            {
                IssueMarkers.Add(new IssueMarker
                {
                    IssueId = issue.Id,
                    Latitude = issue.Location.Coordinates.Latitude,
                    Longitude = issue.Location.Coordinates.Longitude,
                    Title = issue.Title,
                    Status = issue.Status.ToString(),
                    Priority = issue.Priority.ToString()
                });
            }
        }

        // Serialize markers for JavaScript
        MarkersJson = JsonSerializer.Serialize(IssueMarkers);
    }
}

public class IssueMarker
{
    public string IssueId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
}
