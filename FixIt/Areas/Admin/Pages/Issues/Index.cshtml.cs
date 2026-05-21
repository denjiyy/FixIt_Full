using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Models.Issues;
using FixIt.Models.Enums;
using FixIt.Services.Constants;
using FixIt.Services.Contracts;
using System.Security.Claims;

namespace FixIt.Areas.Admin.Pages.Issues;

[Authorize(Policy = PolicyNames.AdminArea)]
public class IndexModel : PageModel
{
    private readonly IIssueService _issueService;
    private readonly ILogger<IndexModel> _logger;

    public List<Issue> Issues { get; set; } = new();
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalIssues { get; set; }
    public int TotalPages { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    public IndexModel(IIssueService issueService, ILogger<IndexModel> logger)
    {
        _issueService = issueService;
        _logger = logger;
    }

    public async Task OnGetAsync(int pageNumber = 1)
    {
        try
        {
            PageNumber = pageNumber;
            IssueStatus? parsedStatus = null;
            if (Enum.TryParse<IssueStatus>(StatusFilter, true, out var status))
            {
                parsedStatus = status;
            }

            var result = await _issueService.GetAllIssuesAsync(
                searchQuery: SearchTerm,
                status: parsedStatus,
                page: PageNumber,
                pageSize: PageSize);

            TotalIssues = (int)result.Total;
            TotalPages = (int)Math.Ceiling((double)result.Total / PageSize);
            Issues = result.Items.ToList();

            _logger.LogInformation("Admin viewed issues list");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading issues");
        }
    }

    public async Task<IActionResult> OnPostResolveAsync(string issueId)
    {
        try
        {
            var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            await _issueService.UpdateIssueStatusAsync(issueId, IssueStatus.Fixed, actorUserId);
            _logger.LogInformation("Issue {IssueId} resolved by admin", issueId);
            TempData["SuccessMessage"] = "Issue marked as resolved.";

            return RedirectToPage(new { pageNumber = PageNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving issue");
            TempData["ErrorMessage"] = "Error resolving issue";
            return RedirectToPage(new { pageNumber = PageNumber });
        }
    }

    public async Task<IActionResult> OnPostReopenAsync(string issueId)
    {
        try
        {
            var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            await _issueService.UpdateIssueStatusAsync(issueId, IssueStatus.InProgress, actorUserId);
            _logger.LogInformation("Issue {IssueId} reopened by admin", issueId);
            TempData["SuccessMessage"] = "Issue reopened.";

            return RedirectToPage(new { pageNumber = PageNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reopening issue");
            TempData["ErrorMessage"] = "Error reopening issue";
            return RedirectToPage(new { pageNumber = PageNumber });
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(string issueId)
    {
        try
        {
            // Only admins can delete issues
            if (!User.IsInRole(RoleNames.Admin))
            {
                _logger.LogWarning("Non-admin user {UserName} attempted to delete issue {IssueId}", User?.Identity?.Name, issueId);
                TempData["ErrorMessage"] = "Only admins can delete issues.";
                return RedirectToPage(new { pageNumber = PageNumber });
            }

            await _issueService.DeleteIssueAsync(issueId);
            _logger.LogWarning("Issue {IssueId} deleted by admin", issueId);
            TempData["SuccessMessage"] = "Issue deleted.";

            return RedirectToPage(new { pageNumber = PageNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting issue");
            TempData["ErrorMessage"] = "Error deleting issue";
            return RedirectToPage(new { pageNumber = PageNumber });
        }
    }
}
