using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Services.Analytics;
using FixIt.Services.Analytics.Models;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Locations;
using System.Text.Json;

namespace FixIt.Pages.HealthReports;

public class HealthReportsModel : PageModel
{
    private readonly IHealthReportService _healthReportService;
    private readonly IRepository<City> _cityRepo;

    public string? CityId { get; set; }
    public bool IsGlobal { get; set; }
    public HealthReport CityReport { get; set; } = new();
    public List<City> AllCities { get; set; } = new();
    public string StatusChartJson { get; set; } = "{}";

    public HealthReportsModel(IHealthReportService healthReportService, IRepository<City> cityRepo)
    {
        _healthReportService = healthReportService;
        _cityRepo = cityRepo;
    }

    public async Task OnGetAsync(string? cityId = null)
    {
        // Get all cities for selector
        var allCitiesEnumerable = await _cityRepo.FindAsync(_ => true);
        AllCities = allCitiesEnumerable.ToList();

        if (string.IsNullOrEmpty(cityId))
        {
            // Get global report
            IsGlobal = true;
            CityReport = await _healthReportService.GetGlobalHealthReportAsync();
        }
        else
        {
            // Get city report
            IsGlobal = false;
            CityId = cityId;
            CityReport = await _healthReportService.GetCityHealthReportAsync(cityId);
        }

        // Prepare chart data - ensure we always have at least one entry
        var statusData = CityReport.IssuesByStatus ?? new Dictionary<string, int>();
        if (statusData.Count == 0)
        {
            statusData["No Data"] = 0;
        }
        StatusChartJson = JsonSerializer.Serialize(statusData);
    }
}
