using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Driver;
using FixIt.Models.Locations;
using FixIt.Models.Issues;

namespace FixIt.Pages;

public class CitiesModel : PageModel
{
    private readonly IMongoDatabase _database;
    private const int PageSize = 9;

    public List<City> Cities { get; set; } = new();
    public List<Issue> CityIssues { get; set; } = new();
    public List<string> Countries { get; set; } = new();
    
    [BindProperty(SupportsGet = true)]
    public string? SelectedCountry { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? SelectedCity { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public int TotalPages { get; set; } = 1;
    public int TotalCities { get; set; } = 0;

    public CitiesModel(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task OnGetAsync()
    {
        var citiesCollection = _database.GetCollection<City>("cities");
        
        // Get unique countries
        var distinctCountries = await citiesCollection
            .Distinct<string>("Country", FilterDefinition<City>.Empty)
            .ToListAsync();
        
        Countries = distinctCountries.Distinct().OrderBy(c => c).ToList();
        
        // If no country selected, default to Bulgaria
        SelectedCountry ??= "Bulgaria";

        // Get cities for selected country
        var countryFilter = Builders<City>.Filter.Eq(c => c.Country, SelectedCountry);
        
        // If city is selected, get issues for that city
        if (!string.IsNullOrEmpty(SelectedCity))
        {
            var issuesCollection = _database.GetCollection<Issue>("issues");
            
            // Get the selected city object
            var selectedCityObj = await citiesCollection
                .Find(Builders<City>.Filter.And(
                    Builders<City>.Filter.Eq(c => c.Name, SelectedCity),
                    Builders<City>.Filter.Eq(c => c.Country, SelectedCountry)
                ))
                .FirstOrDefaultAsync();

            if (selectedCityObj != null)
            {
                // Get issues for this city
                var issueFilter = Builders<Issue>.Filter.And(
                    Builders<Issue>.Filter.Eq(i => i.CityId, selectedCityObj.Id),
                    Builders<Issue>.Filter.Eq(i => i.IsDeleted, false)
                );

                CityIssues = await issuesCollection
                    .Find(issueFilter)
                    .SortByDescending(i => i.CreatedAt)
                    .ToListAsync();
            }
        }

        // Get paginated cities
        var totalCitiesCount = await citiesCollection.CountDocumentsAsync(countryFilter);
        TotalCities = (int)totalCitiesCount;
        TotalPages = (int)Math.Ceiling((double)TotalCities / PageSize);

        // Ensure current page is valid
        if (CurrentPage < 1) CurrentPage = 1;
        if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

        Cities = await citiesCollection
            .Find(countryFilter)
            .SortBy(c => c.Name)
            .Skip((CurrentPage - 1) * PageSize)
            .Limit(PageSize)
            .ToListAsync();
    }

    public IActionResult OnGetSelectCity(string country, string city)
    {
        return RedirectToPage(new { selectedCountry = country, selectedCity = city, currentPage = 1 });
    }
}
