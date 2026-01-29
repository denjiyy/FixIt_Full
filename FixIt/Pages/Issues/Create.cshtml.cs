using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using FixIt.Services.Contracts;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Locations;
using FixIt.Models.Common;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FixIt.Pages.Issues;

[Authorize]
public class CreateIssueModel : PageModel
{
    private readonly IIssueService _issueService;
    private readonly IRepository<City> _cityRepo;

    public CreateIssueModel(
        IIssueService issueService,
        IRepository<City> cityRepo)
    {
        _issueService = issueService;
        _cityRepo = cityRepo;
    }

    [BindProperty]
    public CreateIssueInputModel Input { get; set; } = new();

    public List<SelectListItem> Cities { get; set; } = new();

    public class CreateIssueInputModel
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Description is required")]
        [StringLength(5000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 5000 characters")]
        public string Description { get; set; } = null!;

        [Required(ErrorMessage = "Location is required")]
        public double Latitude { get; set; }

        [Required(ErrorMessage = "Location is required")]
        public double Longitude { get; set; }

        [Required(ErrorMessage = "City is required")]
        public string CityId { get; set; } = null!;

        public string? Address { get; set; }

        public List<IFormFile>? Photos { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadCities();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadCities();
            return Page();
        }

        try
        {
            // Get current user info
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                ModelState.AddModelError("", "User information not available.");
                await LoadCities();
                return Page();
            }

            // Create user summary
            var reporter = new UserSummary
            {
                Id = userId,
                DisplayName = userName ?? "Anonymous"
            };

            // Create the issue
            var issue = await _issueService.CreateIssueAsync(
                title: Input.Title,
                description: Input.Description,
                longitude: Input.Longitude,
                latitude: Input.Latitude,
                cityId: Input.CityId,
                reporter: reporter,
                tagNames: null // TODO: Add tag support if needed
            );

            // TODO: Handle photo uploads if needed

            return RedirectToPage("Detail", new { id = issue.Id });
        }
        catch (Exception ex)
        {
            await LoadCities();
            ModelState.AddModelError("", "Failed to create issue. Please try again.");
            return Page();
        }
    }

    private async Task LoadCities()
    {
        try
        {
            var cities = await _cityRepo.FindAsync(c => c.Country == "Bulgaria");
            var cityList = cities.ToList();
            
            Cities = cityList
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem(c.Name, c.Id))
                .ToList();

            if (!Cities.Any())
            {
                ModelState.AddModelError("", "No cities available in Bulgaria. Please contact support.");
            }
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Failed to load cities: {ex.Message}");
            Cities = new List<SelectListItem>();
        }
    }
}
