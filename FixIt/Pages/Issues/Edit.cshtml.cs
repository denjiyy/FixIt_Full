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
using FixIt.Services.Constants;

namespace FixIt.Pages.Issues;

[Authorize]
public class EditIssueModel : PageModel
{
    private readonly IIssueService _issueService;
    private readonly IRepository<City> _cityRepo;
    private readonly ILogger<EditIssueModel> _logger;
    private readonly IMediaService _mediaService;

    public EditIssueModel(
        IIssueService issueService,
        IRepository<City> cityRepo,
        ILogger<EditIssueModel> logger,
        IMediaService mediaService)
    {
        _issueService = issueService;
        _cityRepo = cityRepo;
        _logger = logger;
        _mediaService = mediaService;
    }

    public Issue? Issue { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = null!;

    [BindProperty]
    public EditIssueInputModel Input { get; set; } = new();

    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Cities { get; set; } = new();
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Priorities { get; set; } = new();
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> Statuses { get; set; } = new();

    // Media properties
    public List<(string Id, string? ThumbnailUrl, string? FileName)> ExistingMedia { get; set; } = new();

    [BindProperty]
    public List<IFormFile>? NewMediaFiles { get; set; }

    public class EditIssueInputModel
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Description is required")]
        [StringLength(5000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 5000 characters")]
        public string Description { get; set; } = null!;

        public string? Address { get; set; }

        public IssuePriority Priority { get; set; } = IssuePriority.Medium;

        public IssueStatus Status { get; set; } = IssueStatus.New;

        [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180")]
        public double? Longitude { get; set; }

        [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90")]
        public double? Latitude { get; set; }

        // Media management
        public List<string> ExistingMediaIds { get; set; } = new();
        public List<string> MediaIdsToRemove { get; set; } = new();
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
            if (Issue.Reporter.Id != userId && !User.IsInRole(RoleNames.Admin))
            {
                return Forbid();
            }

            // Populate form with current data
            Input = new EditIssueInputModel
            {
                Title = Issue.Title,
                Description = Issue.Description,
                Address = Issue.Address,
                Priority = Issue.Priority,
                Status = Issue.Status,
                Latitude = Issue.Location?.Coordinates?.Latitude,
                Longitude = Issue.Location?.Coordinates?.Longitude,
                ExistingMediaIds = Issue.MediaIds.ToList()
            };

            await LoadCities();
            LoadPriorities();
            LoadStatuses();
            await LoadMediaFiles();

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
            LoadPriorities();
            LoadStatuses();
            await LoadMediaFiles();
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
            if (issue.Reporter.Id != userId && !User.IsInRole(RoleNames.Admin))
            {
                return Forbid();
            }

            // Handle new media uploads
            var newMediaIds = new List<string>();
            if (NewMediaFiles != null && NewMediaFiles.Any())
            {
                try
                {
                    var uploadedMedia = await _mediaService.UploadFilesAsync(
                        NewMediaFiles, 
                        userId, 
                        MediaReferenceType.Issue, 
                        Id);
                    newMediaIds = uploadedMedia.Select(m => m.Id).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading media files");
                    ModelState.AddModelError("", "Failed to upload some media files");
                    await LoadCities();
                    LoadPriorities();
                    LoadStatuses();
                    await LoadMediaFiles();
                    return Page();
                }
            }

            // Call the service method to update issue details
            await _issueService.UpdateIssueDetailsAsync(
                Id,
                Input.Title,
                Input.Description,
                Input.Address,
                Input.Priority,
                Input.Status,
                Input.Latitude,
                Input.Longitude,
                newMediaIds.Any() ? newMediaIds : null,
                Input.MediaIdsToRemove.Any() ? Input.MediaIdsToRemove : null,
                userId);

            _logger.LogInformation("Issue {IssueId} updated by user {UserId}", Id, userId);

            TempData["SuccessMessage"] = "Issue updated successfully";
            return RedirectToPage("./Detail", new { id = Id });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error updating issue {IssueId}", Id);
            ModelState.AddModelError("", ex.Message);
            await LoadCities();
            LoadPriorities();
            LoadStatuses();
            await LoadMediaFiles();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating issue {IssueId}", Id);
            ModelState.AddModelError("", "Failed to update issue");
            await LoadCities();
            LoadPriorities();
            LoadStatuses();
            await LoadMediaFiles();
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

    private void LoadPriorities()
    {
        Priorities = System.Enum.GetValues(typeof(IssuePriority))
            .Cast<IssuePriority>()
            .Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = p.ToString(),
                Text = p.ToString()
            })
            .ToList();
    }

    private void LoadStatuses()
    {
        Statuses = System.Enum.GetValues(typeof(IssueStatus))
            .Cast<IssueStatus>()
            .Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = s.ToString(),
                Text = s.ToString()
            })
            .ToList();
    }

    private async Task LoadMediaFiles()
    {
        if (Issue?.MediaIds == null || !Issue.MediaIds.Any())
        {
            ExistingMedia = new List<(string, string?, string?)>();
            return;
        }

        try
        {
            // Fetch media for the issue using the service
            var mediaList = await _mediaService.GetMediaForReferenceAsync(
                MediaReferenceType.Issue, 
                Id);

            ExistingMedia = mediaList
                .Select(m => (m.Id, m.ThumbnailPath, (string?)m.Id)) // Cast Id to string? for tuple compatibility
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading media files for issue {IssueId}", Id);
            ExistingMedia = new List<(string, string?, string?)>();
        }
    }
}

