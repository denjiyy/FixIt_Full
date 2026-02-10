using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using FixIt.Services.Contracts;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.Locations;
using FixIt.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace FixIt.Pages.Issues;

[Authorize]
public class EditIssueModel : PageModel
{
    private readonly IIssueService _issueService;
    private readonly IRepository<City> _cityRepo;
    private readonly ILogger<EditIssueModel> _logger;

    public EditIssueModel(
        IIssueService issueService,
        IRepository<City> cityRepo,
        ILogger<EditIssueModel> logger)
    {
        _issueService = issueService;
        _cityRepo = cityRepo;
        _logger = logger;
    }

    public Issue? Issue { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = null!;

    [BindProperty]
    public EditIssueInputModel Input { get; set; } = new();

    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Cities { get; set; } = new();

    public class EditIssueInputModel
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Description is required")]
        [StringLength(5000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 5000 characters")]
        public string Description { get; set; } = null!;

        public string? Address { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrEmpty(Id))
        {
            return NotFound();
        }

        try
        {
            Issue = await _issueService.GetIssueByIdAsync(Id);
            
            if (Issue == null)
            {
                return NotFound();
            }

            // Check if user is the owner or admin
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Issue.Reporter.Id != userId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            // Populate form with current data
            Input = new EditIssueInputModel
            {
                Title = Issue.Title,
                Description = Issue.Description,
                Address = Issue.Address
            };

            await LoadCities();

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading issue {IssueId}", Id);
            return NotFound();
        }
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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var issue = await _issueService.GetIssueByIdAsync(Id);
            if (issue == null)
            {
                return NotFound();
            }

            // Check ownership or admin
            if (issue.Reporter.Id != userId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            // Update issue properties
            issue.Title = Input.Title;
            issue.Description = Input.Description;
            issue.Address = Input.Address;
            issue.UpdatedAt = DateTime.UtcNow;
            issue.LastActivityAt = DateTime.UtcNow;

            await _issueService.UpdateIssueAsync(issue);

            _logger.LogInformation("Issue {IssueId} updated by user {UserId}", Id, userId);

            TempData["SuccessMessage"] = "Issue updated successfully";
            return RedirectToPage("./Detail", new { id = Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating issue {IssueId}", Id);
            ModelState.AddModelError("", "Failed to update issue");
            await LoadCities();
            return Page();
        }
    }

    private async Task LoadCities()
    {
        var cities = await _cityRepo.FindAsync(c => true);
        Cities = cities
            .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id,
                Text = c.Name
            })
            .ToList();
    }

}

