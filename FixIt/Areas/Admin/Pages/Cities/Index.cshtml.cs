using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Models.Locations;
using FixIt.Data.Repository.Contracts;

namespace FixIt.Areas.Admin.Pages.Cities;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly IRepository<City> _cityRepository;
    private readonly ILogger<IndexModel> _logger;

    public List<City> Cities { get; set; } = new();
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalCities { get; set; }
    public int TotalPages { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty]
    public string CityName { get; set; } = string.Empty;

    [BindProperty]
    public string Country { get; set; } = string.Empty;

    [BindProperty]
    public double Latitude { get; set; }

    [BindProperty]
    public double Longitude { get; set; }

    [BindProperty]
    public string Description { get; set; } = string.Empty;

    [BindProperty]
    public string PhotoUrl { get; set; } = string.Empty;

    public IndexModel(IRepository<City> cityRepository, ILogger<IndexModel> logger)
    {
        _cityRepository = cityRepository;
        _logger = logger;
    }

    public async Task OnGetAsync(int pageNumber = 1)
    {
        try
        {
            PageNumber = pageNumber;
            var allCities = await _cityRepository.FindAsync(c => true);
            var citiesList = allCities.ToList();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                citiesList = citiesList.Where(c => 
                    c.Name.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    c.Country.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            TotalCities = citiesList.Count;
            TotalPages = (int)Math.Ceiling(TotalCities / (double)PageSize);

            Cities = citiesList
                .OrderBy(c => c.Country)
                .ThenBy(c => c.Name)
                .Skip((pageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            _logger.LogInformation("Admin viewed cities list");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading cities");
        }
    }

    public async Task<IActionResult> OnPostAddCityAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CityName) || string.IsNullOrWhiteSpace(Country) || (Latitude == 0 && Longitude == 0))
            {
                TempData["ErrorMessage"] = "Please fill in all required fields including map coordinates.";
                return RedirectToPage();
            }

            var newCity = new City
            {
                Name = CityName,
                Country = Country,
                Latitude = Latitude,
                Longitude = Longitude,
                Description = Description,
                PhotoUrl = PhotoUrl,
                CreatedAt = DateTime.UtcNow
            };

            await _cityRepository.InsertAsync(newCity);

            _logger.LogInformation($"City {CityName} added by admin");
            TempData["SuccessMessage"] = $"{CityName} has been added successfully.";

            // Reset form
            CityName = string.Empty;
            Country = string.Empty;
            Latitude = 0;
            Longitude = 0;
            Description = string.Empty;
            PhotoUrl = string.Empty;

            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding city");
            TempData["ErrorMessage"] = $"Error adding city: {ex.Message}";
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(string cityId)
    {
        try
        {
            var city = await _cityRepository.GetByIdAsync(cityId);
            if (city == null)
                return NotFound();

            await _cityRepository.DeleteAsync(cityId);

            _logger.LogWarning($"City {city.Name} deleted by admin");
            TempData["SuccessMessage"] = $"{city.Name} has been deleted.";

            return RedirectToPage(new { pageNumber = PageNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting city");
            TempData["ErrorMessage"] = "Error deleting city";
            return RedirectToPage(new { pageNumber = PageNumber });
        }
    }
}
