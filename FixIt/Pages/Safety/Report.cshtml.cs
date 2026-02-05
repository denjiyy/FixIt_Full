using FixIt.Models.Safety;
using FixIt.Services.Safety;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using FixIt.Models.Locations;
using FixIt.Data.Repository.Contracts;

namespace FixIt.Pages.Safety;

[Authorize]
public class ReportModel : PageModel
{
    private readonly IHazardService _hazardService;
    private readonly IRepository<City> _cityRepo;

    [BindProperty]
    [Required]
    public HazardType HazardType { get; set; }

    [BindProperty]
    [Required]
    public HazardSeverity Severity { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Description is required")]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [Range(-90, 90)]
    public double Latitude { get; set; }

    [BindProperty]
    [Required]
    [Range(-180, 180)]
    public double Longitude { get; set; }

    [BindProperty]
    [StringLength(300)]
    public string? Address { get; set; }

    [BindProperty]
    public bool IsAnonymous { get; set; }

    public List<City> Cities { get; set; } = new();

    public ReportModel(IHazardService hazardService, IRepository<City> cityRepo)
    {
        _hazardService = hazardService;
        _cityRepo = cityRepo;
    }

    public async Task OnGetAsync()
    {
        var cities = await _cityRepo.FindAsync(c => true);
        Cities = cities.ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return RedirectToPage("/Login");

            // Get first city or default city (in production, user should select city)
            var cities = await _cityRepo.FindAsync(c => true);
            var cityId = cities.FirstOrDefault()?.Id ?? "";

            if (string.IsNullOrEmpty(cityId))
            {
                ModelState.AddModelError("", "No cities available. Please contact administrator.");
                return Page();
            }

            var hazard = await _hazardService.CreateHazardAsync(
                cityId: cityId,
                type: HazardType,
                severity: Severity,
                title: Title,
                description: Description,
                latitude: Latitude,
                longitude: Longitude,
                address: Address,
                userId: userId,
                isAnonymous: IsAnonymous
            );

            TempData["SuccessMessage"] = "Hazard reported successfully. Thank you for helping keep your community safe!";
            return RedirectToPage("/Safety/View", new { hazardId = hazard.Id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Error reporting hazard: {ex.Message}");
            return Page();
        }
    }
}