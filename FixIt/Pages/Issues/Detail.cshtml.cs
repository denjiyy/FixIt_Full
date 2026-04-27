using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using FixIt.Services.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.AI;
using FixIt.Models.Engagement;
using FixIt.Services.AI;
using FixIt.Services.Background;
using FixIt.Services.Constants;

namespace FixIt.Pages.Issues;

public class IssueDetailModel : PageModel
{
    private readonly IIssueService _issueService;
    private readonly IMediaService _mediaService;
    private readonly IIssueAnalysisService _analysisService;
    private readonly IIssueAnalysisQueue _issueAnalysisQueue;
    private readonly ILogger<IssueDetailModel> _logger;

    public IssueDetailModel(
        IIssueService issueService, 
        IMediaService mediaService,
        IIssueAnalysisService analysisService,
        IIssueAnalysisQueue issueAnalysisQueue,
        ILogger<IssueDetailModel> logger)
    {
        _issueService = issueService;
        _mediaService = mediaService;
        _analysisService = analysisService;
        _issueAnalysisQueue = issueAnalysisQueue;
        _logger = logger;
    }

    public Issue? Issue { get; set; }
    public IssueAnalysis? Analysis { get; set; }
    public List<FixIt.Models.Media.Media> MediaList { get; set; } = new();
    public List<Comment> Comments { get; set; } = new();
    public int? IssueCount { get; set; }
    public bool CanManageIssue { get; set; }
    
    public List<IssueStatusHistory> SortedStatusHistory
    {
        get
        {
            return Issue?.StatusHistory?
                .OrderByDescending(history => history.ChangedAt)
                .ToList() ?? new List<IssueStatusHistory>();
        }
    }

    // IMPORTANT: Add route parameter binding
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrEmpty(Id))
        {
            return NotFound();
        }

        try
        {
            // Load issue from service (no automatic view increment)
            Issue = await _issueService.GetIssueByIdAsync(Id);
            
            if (Issue == null)
            {
                return NotFound();
            }

            // Track the view - only increment if it's a new view from this user/session
            // This runs synchronously so the view count is updated before the page renders
            try
            {
                var userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var sessionId = HttpContext.Session?.Id ?? "anonymous";
                var ipAddress = HttpContext.Connection?.RemoteIpAddress?.ToString();
                
                // Track the view and reload the issue with updated count
                await _issueService.TrackViewAsync(Id, userId, sessionId, ipAddress);
                
                // Reload the issue to get the updated view count
                Issue = await _issueService.GetIssueByIdAsync(Id);
            }
            catch (Exception trackingEx)
            {
                // Log but don't fail - view tracking errors shouldn't break the page
                _logger.LogWarning(trackingEx, "View tracking error: {Message}", trackingEx.Message);
            }

            // Load AI analysis if available, otherwise trigger it
            Analysis = await _analysisService.GetAnalysisAsync(Id);
            
            // If no analysis exists, trigger it in the background
            if (Analysis == null)
            {
                try
                {
                    await _issueAnalysisQueue.QueueAnalysisAsync(Id);
                }
                catch (Exception enqueueEx)
                {
                    _logger.LogWarning(enqueueEx, "Failed to queue analysis for issue {IssueId}", Id);
                }
            }
            
            // Load media files
            MediaList = await _mediaService.GetMediaForReferenceAsync(
                FixIt.Models.Enums.MediaReferenceType.Issue, 
                Id
            );
            
            // Load comments
            Comments = await _issueService.GetCommentsForIssueAsync(Id);

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            CanManageIssue = !string.IsNullOrWhiteSpace(currentUserId) &&
                (User.IsInRole(RoleNames.Admin) || Issue.Reporter?.Id == currentUserId);

            if (!(Issue?.IsAnonymous ?? true) && !string.IsNullOrWhiteSpace(Issue?.Reporter?.Id))
            {
                var reporterIssues = await _issueService.GetUserIssuesAsync(Issue.Reporter.Id, 1, 1);
                IssueCount = (int)reporterIssues.Total;
            }
            
            // Set flag for Leaflet CSS loading in _Layout.cshtml
            if (Issue?.Location?.Coordinates != null && Issue.Location.Coordinates.Values?.Count >= 2)
            {
                ViewData["RequiresMap"] = true;
            }
            
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading issue detail page for {IssueId}", Id);
            return NotFound();
        }
    }

    [Authorize]
    public async Task<IActionResult> OnPostAddCommentAsync(string commentText)
    {
        if (string.IsNullOrWhiteSpace(commentText))
        {
            ModelState.AddModelError("", "Comment cannot be empty");
            return RedirectToPage(new { id = Id });
        }

        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Check if user has enabled anonymous reporting in their privacy settings
            // Comments respect the same privacy setting as issues
            var _userManager = HttpContext.RequestServices.GetService(typeof(Microsoft.AspNetCore.Identity.UserManager<FixIt.Models.Users.ApplicationUser>)) as Microsoft.AspNetCore.Identity.UserManager<FixIt.Models.Users.ApplicationUser>;
            var user = await _userManager?.GetUserAsync(User)!;
            bool isAnonymous = user?.AnonymousReportingEnabled ?? false;

            // Add comment via service
            await _issueService.AddCommentAsync(Id, userId, commentText, isAnonymous);

            return RedirectToPage(new { id = Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return RedirectToPage(new { id = Id });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return RedirectToPage(new { id = Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to issue {IssueId}", Id);
            ModelState.AddModelError("", "Failed to add comment");
            return RedirectToPage(new { id = Id });
        }
    }
}
