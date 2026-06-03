using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using FixIt.Services.Contracts;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Locations;
using FixIt.Models.Common;
using FixIt.Models.Issues;
using FixIt.Models.Users;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FixIt.Models.Enums;
using FixIt.Models.AI;
using FixIt.Services.Constants;

namespace FixIt.Pages.Issues;

[Authorize]
[EnableRateLimiting(RateLimitPolicyNames.Upload)]
public class CreateIssueModel : PageModel
{
    private readonly IIssueService _issueService;
    private readonly IMediaService _mediaService;
    private readonly IRepository<City> _cityRepo;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CreateIssueModel> _logger;

    public CreateIssueModel(
        IIssueService issueService,
        IMediaService mediaService,
        IRepository<City> cityRepo,
        UserManager<ApplicationUser> userManager,
        ILogger<CreateIssueModel> logger)
    {
        _issueService = issueService;
        _mediaService = mediaService;
        _cityRepo = cityRepo;
        _userManager = userManager;
        _logger = logger;
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
        public double? Latitude { get; set; }

        [Required(ErrorMessage = "Location is required")]
        public double? Longitude { get; set; }

        [Required(ErrorMessage = "City is required")]
        public string CityId { get; set; } = null!;

        public string? Address { get; set; }

        public string? TagNames { get; set; }

        public IssuePriority? Priority { get; set; }

        public IssueCategory? Category { get; set; }

        [StringLength(120, ErrorMessage = "Department must not exceed 120 characters")]
        public string? Department { get; set; }

        /// <summary>Opt-in per-report anonymity (OR-ed with the user's privacy setting).</summary>
        public bool IsAnonymous { get; set; }

        public List<IFormFile>? Photos { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadCities();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Debug: Log received values
        _logger.LogInformation("Form submission received:");
        _logger.LogInformation("  Title: '{Title}' (length: {TitleLength})", Input.Title, Input.Title?.Length ?? 0);
        _logger.LogInformation("  Description: '{Description}' (length: {DescLength})", Input.Description, Input.Description?.Length ?? 0);
        _logger.LogInformation("  Latitude: {Latitude}, Longitude: {Longitude}", Input.Latitude, Input.Longitude);
        _logger.LogInformation("  CityId: '{CityId}'", Input.CityId);
        _logger.LogInformation("  ModelState.IsValid: {IsValid}", ModelState.IsValid);
        
        // Log validation errors
        if (!ModelState.IsValid)
        {
            foreach (var modelState in ModelState.Values)
            {
                foreach (var error in modelState.Errors)
                {
                    _logger.LogInformation("  ValidationError: {Error}", error.ErrorMessage);
                }
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadCities();
            return Page();
        }

        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                ModelState.AddModelError("", "User information not available.");
                await LoadCities();
                return Page();
            }

            var reporter = new UserSummary
            {
                Id = userId,
                DisplayName = userName ?? "Anonymous"
            };

            // Anonymity: opt-in per report, or forced on by the user's privacy settings.
            var user = await _userManager.GetUserAsync(User);
            bool isAnonymous = Input.IsAnonymous || (user?.AnonymousReportingEnabled ?? false);

            // Create the issue
            var tagNames = !string.IsNullOrWhiteSpace(Input.TagNames)
                ? Input.TagNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim().ToLowerInvariant())
                    .Distinct()
                    .ToList()
                : new List<string>();

            var issue = await _issueService.CreateIssueAsync(
                title: Input.Title ?? "",
                description: Input.Description ?? "",
                longitude: Input.Longitude!.Value,
                latitude: Input.Latitude!.Value,
                cityId: Input.CityId,
                reporter: reporter,
                tagNames: tagNames,
                isAnonymous: isAnonymous,
                priority: Input.Priority,
                category: Input.Category,
                department: string.IsNullOrWhiteSpace(Input.Department) ? null : Input.Department.Trim(),
                address: Input.Address
            );

            _logger.LogInformation("Created issue {IssueId} by user {UserId}", issue.Id, userId);

            // Handle photo/video uploads
            if (Input.Photos != null && Input.Photos.Any())
            {
                try
                {
                    var uploadedMedia = await _mediaService.UploadFilesAsync(
                        Input.Photos,
                        userId,
                        MediaReferenceType.Issue,
                        issue.Id
                    );

                    foreach (var media in uploadedMedia)
                    {
                        issue.MediaIds.Add(media.Id);
                    }

                    _logger.LogInformation("Uploaded {Count} media files for issue {IssueId}", 
                        uploadedMedia.Count, issue.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload media for issue {IssueId}", issue.Id);
                    TempData["Warning"] = "Issue created but some media failed to upload.";
                }
            }

            // Save final issue state
            await _issueService.UpdateIssueAsync(issue);

            TempData["Success"] = "Issue reported successfully!";

            return Redirect($"/issues/{issue.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create issue");
            await LoadCities();
            ModelState.AddModelError("", "Failed to create issue");
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
                _logger.LogWarning("No cities found in Bulgaria");
                ModelState.AddModelError("", "No cities available. Please contact support.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load cities");
            ModelState.AddModelError("", "Failed to load cities. Please try again.");
            Cities = new List<SelectListItem>();
        }
    }
}
