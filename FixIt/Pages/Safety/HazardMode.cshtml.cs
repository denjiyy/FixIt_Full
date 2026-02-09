using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Models.Locations;
using FixIt.Data.Repository.Contracts;

namespace FixIt.Pages.Safety;

public class HazardModeModel : PageModel
{
    private readonly IRepository<City> _cityRepo;

    public string CityId { get; set; } = string.Empty;
    public City? City { get; set; }

    public HazardModeModel(IRepository<City> cityRepo)
    {
        _cityRepo = cityRepo;
    }

    public async Task OnGetAsync(string? cityId)
    {
        // Get cities
        var cities = await _cityRepo.FindAsync(c => true);
        
        if (!string.IsNullOrEmpty(cityId))
        {
            City = cities.FirstOrDefault(c => c.Id == cityId);
            CityId = cityId;
        }
        else
        {
            City = cities.FirstOrDefault();
            if (City != null)
                CityId = City.Id;
        }
    }
}
