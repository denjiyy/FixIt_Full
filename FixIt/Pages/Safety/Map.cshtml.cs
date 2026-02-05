using FixIt.Models.Safety;
using FixIt.Models.Locations;
using FixIt.Services.Safety;
using FixIt.Data.Repository.Contracts;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace FixIt.Pages.Safety;

public class MapModel : PageModel
{
    private readonly IHazardService _hazardService;
    private readonly IRepository<City> _cityRepo;

    public List<Hazard> Hazards { get; set; } = new();
    public string HazardsJson { get; set; } = "[]";

    public MapModel(IHazardService hazardService, IRepository<City> cityRepo)
    {
        _hazardService = hazardService;
        _cityRepo = cityRepo;
    }

    public async Task OnGetAsync(string? cityId)
    {
        // Get cities
        var cities = await _cityRepo.FindAsync(c => true);
        var targetCity = !string.IsNullOrEmpty(cityId)
            ? cities.FirstOrDefault(c => c.Id == cityId)
            : cities.FirstOrDefault();

        if (targetCity != null)
        {
            Hazards = await _hazardService.GetCityHazardsAsync(targetCity.Id, includeResolved: false);
        }

        // Serialize for JavaScript
        var hazardData = Hazards.Select(h => new
        {
            id = h.Id,
            title = h.Title,
            type = h.Type.ToString(),
            severity = h.Severity.ToString(),
            description = h.Description,
            latitude = h.Location.Coordinates.Latitude,
            longitude = h.Location.Coordinates.Longitude,
            address = h.Address ?? "Unknown location",
            confirmations = h.Confirmations,
            isResolved = h.IsResolved,
            reporter = h.IsAnonymous ? "Anonymous" : h.ReportedByUserId ?? "Unknown"
        }).ToList();

        HazardsJson = JsonSerializer.Serialize(hazardData);
    }
}
