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
    private readonly IAuditService _auditService;

    public List<Issue> Issues { get; set; } = new();
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalIssues { get; set; }
    public int TotalPages { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    public IndexModel(IIssueService issueService, ILogger<IndexModel> logger, IAuditService auditService)
    {
        _issueService = issueService;
        _logger = logger;
        _auditService = auditService;
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
            var issue = await _issueService.GetIssueByIdAsync(issueId);
            
            if (issue == null)
            {
                TempData["ErrorMessage"] = "Issue not found";
                return RedirectToPage(new { pageNumber = PageNumber });
            }

            var oldStatus = issue.Status;

            // Follow state machine transitions to reach Fixed status
            if (issue.Status == IssueStatus.New)
            {
                await _issueService.UpdateIssueStatusAsync(issueId, IssueStatus.Confirmed, actorUserId, "Confirmed by admin");
                await _issueService.UpdateIssueStatusAsync(issueId, IssueStatus.InProgress, actorUserId, "Marked in progress by admin");
                await _issueService.UpdateIssueStatusAsync(issueId, IssueStatus.Fixed, actorUserId, "Resolved by admin");
            }
            else if (issue.Status == IssueStatus.Confirmed)
            {
                await _issueService.UpdateIssueStatusAsync(issueId, IssueStatus.InProgress, actorUserId, "Marked in progress by admin");
                await _issueService.UpdateIssueStatusAsync(issueId, IssueStatus.Fixed, actorUserId, "Resolved by admin");
            }
            else if (issue.Status == IssueStatus.InProgress)
            {
                await _issueService.UpdateIssueStatusAsync(issueId, IssueStatus.Fixed, actorUserId, "Resolved by admin");
            }
            else if (issue.Status == IssueStatus.Fixed || issue.Status == IssueStatus.Archived)
            {
                TempData["WarningMessage"] = $"Issue is already {issue.Status}";
                return RedirectToPage(new { pageNumber = PageNumber });
            }
            else if (issue.Status == IssueStatus.Rejected || issue.Status == IssueStatus.Duplicate)
            {
                TempData["ErrorMessage"] = $"Cannot resolve a {issue.Status} issue";
                return RedirectToPage(new { pageNumber = PageNumber });
            }

            _logger.LogInformation("Issue {IssueId} resolved by admin (transitioned from {OldStatus})", issueId, oldStatus);
            
            await _auditService.LogEventAsync(
                eventType: "IssueStatusChanged",
                action: "ResolveIssue",
                resource: "Issue",
                resourceId: issueId,
                changes: new Dictionary<string, object>
                {
                    { "OldStatus", oldStatus },
                    { "NewStatus", IssueStatus.Fixed }
                },
                status: "Success"
            );
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
            var oldIssue = await _issueService.GetIssueByIdAsync(issueId);
            await _issueService.UpdateIssueStatusAsync(issueId, IssueStatus.InProgress, actorUserId);
            _logger.LogInformation("Issue {IssueId} reopened by admin", issueId);
            
            await _auditService.LogEventAsync(
                eventType: "IssueStatusChanged",
                action: "UpdateStatus",
                resource: "Issue",
                resourceId: issueId,
                changes: new Dictionary<string, object>
                {
                    { "OldStatus", oldIssue?.Status ?? IssueStatus.New },
                    { "NewStatus", IssueStatus.InProgress }
                },
                status: "Success"
            );
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
            if (!User.IsInRole(RoleNames.Admin))
            {
                _logger.LogWarning("Non-admin user {UserName} attempted to delete issue {IssueId}", User?.Identity?.Name, issueId);
                TempData["ErrorMessage"] = "Only admins can delete issues.";
                return RedirectToPage(new { pageNumber = PageNumber });
            }

            var issue = await _issueService.GetIssueByIdAsync(issueId);
            await _issueService.DeleteIssueAsync(issueId);
            _logger.LogWarning("Issue {IssueId} deleted by admin", issueId);
            
            await _auditService.LogEventAsync(
                eventType: "IssueDeleted",
                action: "Delete",
                resource: "Issue",
                resourceId: issueId,
                changes: new Dictionary<string, object>
                {
                    { "Title", issue?.Title ?? "Unknown" },
                    { "Status", issue?.Status ?? IssueStatus.New }
                },
                status: "Success"
            );
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
