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
    public double CityLatitude { get; set; }
    public double CityLongitude { get; set; }
    public List<IssueMarker> IssueMarkers { get; set; } = new();
    public string MarkersJson { get; set; } = "[]";
    public int TotalIssues { get; set; }
    public int OpenIssues { get; set; }
    public int ResolvedIssues { get; set; }
    
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
        CityLatitude = city.Latitude;
        CityLongitude = city.Longitude;

        Console.WriteLine($"[Heatmaps] City: {city.Name}, Lat: {city.Latitude}, Lng: {city.Longitude}");

        // Get all issues for this city
        var issueRepo = HttpContext.RequestServices.GetRequiredService<IRepository<FixIt.Models.Issues.Issue>>();
        var allIssues = (await issueRepo.FindAsync(i => i.CityId == cityId)).ToList();

        Console.WriteLine($"[Heatmaps] Found {allIssues.Count} issues for city {cityId}");

        // Calculate statistics
        TotalIssues = allIssues.Count;
        OpenIssues = allIssues.Count(i => i.Status == FixIt.Models.Enums.IssueStatus.New || 
                                          i.Status == FixIt.Models.Enums.IssueStatus.InProgress ||
                                          i.Status == FixIt.Models.Enums.IssueStatus.Confirmed);
        ResolvedIssues = allIssues.Count(i => i.Status == FixIt.Models.Enums.IssueStatus.Fixed ||
                                             i.Status == FixIt.Models.Enums.IssueStatus.Rejected);

        // Create markers for all issues
        IssueMarkers = new List<IssueMarker>();
        foreach (var issue in allIssues)
        {
            if (issue.Location?.Coordinates != null)
            {
                Console.WriteLine($"[Heatmaps] Issue: {issue.Title}, Lat: {issue.Location.Coordinates.Latitude}, Lng: {issue.Location.Coordinates.Longitude}");
                
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
            else
            {
                Console.WriteLine($"[Heatmaps] Issue {issue.Title} has no location coordinates!");
            }
        }

        Console.WriteLine($"[Heatmaps] Created {IssueMarkers.Count} markers");

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
