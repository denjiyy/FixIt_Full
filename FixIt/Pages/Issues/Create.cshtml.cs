using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using FixIt.Services.Contracts;
using System.ComponentModel.DataAnnotations;

namespace FixIt.Pages.Issues;

[Authorize]
public class CreateIssueModel : PageModel
{
    private readonly IIssueService _issueService;

    public CreateIssueModel(IIssueService issueService)
    {
        _issueService = issueService;
    }

    [BindProperty]
    public CreateIssueInputModel Input { get; set; } = new();

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

        [Required(ErrorMessage = "Neighborhood is required")]
        public string NeighborhoodId { get; set; } = null!;

        public string? Address { get; set; }

        public List<IFormFile>? Photos { get; set; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            // TODO: Implement issue creation with photo upload
            // For now, just redirect
            return RedirectToPage("Index");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Failed to create issue. Please try again.");
            return Page();
        }
    }
}
