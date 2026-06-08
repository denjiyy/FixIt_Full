using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Services.Contracts;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Locations;
using System.Text.Json;

namespace FixIt.Pages.Heatmaps;

public class HeatmapsModel : PageModel
{
    private readonly IIssueService _issueService;
    private readonly IRepository<City> _cityRepo;

    public string CityName { get; set; } = string.Empty;
    public double CityLatitude { get; set; }
    public double CityLongitude { get; set; }
    public List<IssueMarker> IssueMarkers { get; set; } = new();
    public string MarkersJson { get; set; } = "[]";
    public int TotalIssues { get; set; }
    public int OpenIssues { get; set; }
    public int ResolvedIssues { get; set; }
    
    public HeatmapsModel(
        IIssueService issueService,
        IRepository<City> cityRepo)
    {
        _issueService = issueService;
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

        // Get all issues for this city
        var allIssues = await _issueService.GetIssuesByCityAsync(cityId);

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
            // GeoJsonPoint stores coordinates as Longitude, Latitude
            if (issue.Location != null && issue.Location.Coordinates != null)
            {
                try
                {
                    var coordinates = issue.Location.Coordinates;
                    
                    // Serialize the coordinates object to JSON to extract latitude/longitude
                    var coordJson = JsonSerializer.Serialize(coordinates);
                    
                    // Parse the JSON to extract latitude/longitude
                    using var jsonDoc = System.Text.Json.JsonDocument.Parse(coordJson);
                    var root = jsonDoc.RootElement;
                    
                    double latitude = 0;
                    double longitude = 0;
                    
                    // Try common property names
                    if (root.TryGetProperty("latitude", out var latProp))
                        latitude = latProp.GetDouble();
                    if (root.TryGetProperty("Latitude", out latProp))
                        latitude = latProp.GetDouble();
                    
                    if (root.TryGetProperty("longitude", out var lngProp))
                        longitude = lngProp.GetDouble();
                    if (root.TryGetProperty("Longitude", out lngProp))
                        longitude = lngProp.GetDouble();
                    
                    if (latitude != 0 && longitude != 0)
                    {
                        IssueMarkers.Add(new IssueMarker
                        {
                            IssueId = issue.Id,
                            Latitude = latitude,
                            Longitude = longitude,
                            Title = issue.Title,
                            Status = issue.Status.ToString(),
                            Priority = issue.Priority.ToString()
                        });
                    }
                }
                catch (Exception)
                {
                    // Silently fail - issue will not be shown on map
                }
            }
        }

        // Serialize markers for JavaScript with camelCase property names
        var options = new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        MarkersJson = JsonSerializer.Serialize(IssueMarkers, options);
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
