using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Models.Issues;
using FixIt.Models.Enums;
using FixIt.Data.Repository.Contracts;

namespace FixIt.Areas.Admin.Pages.Issues;

[Authorize(Roles = "Admin,Moderator")]
public class IndexModel : PageModel
{
    private readonly IRepository<Issue> _issueRepository;
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

    public IndexModel(IRepository<Issue> issueRepository, ILogger<IndexModel> logger)
    {
        _issueRepository = issueRepository;
        _logger = logger;
    }

    public async Task OnGetAsync(int pageNumber = 1)
    {
        try
        {
            PageNumber = pageNumber;
            var allIssues = await _issueRepository.FindAsync(i => true);
            var issuesList = allIssues.ToList();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                issuesList = issuesList.Where(i => 
                    i.Title.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    i.Description.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            TotalIssues = issuesList.Count;
            TotalPages = (int)Math.Ceiling(TotalIssues / (double)PageSize);

            Issues = issuesList
                .OrderByDescending(i => i.CreatedAt)
                .Skip((pageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();

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
            var issue = await _issueRepository.GetByIdAsync(issueId);
            if (issue == null)
                return NotFound();

            issue.Status = IssueStatus.Fixed;
            issue.UpdatedAt = DateTime.UtcNow;

            await _issueRepository.ReplaceAsync(issueId, issue);

            _logger.LogInformation($"Issue {issue.Id} resolved by admin");
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
            var issue = await _issueRepository.GetByIdAsync(issueId);
            if (issue == null)
                return NotFound();

            issue.Status = IssueStatus.InProgress;
            issue.UpdatedAt = DateTime.UtcNow;

            await _issueRepository.ReplaceAsync(issueId, issue);

            _logger.LogInformation($"Issue {issue.Id} reopened by admin");
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
            if (!User.IsInRole("Admin"))
            {
                _logger.LogWarning($"Moderator {User?.Identity?.Name} attempted to delete issue {issueId}");
                TempData["ErrorMessage"] = "Only admins can delete issues.";
                return RedirectToPage(new { pageNumber = PageNumber });
            }

            var issue = await _issueRepository.GetByIdAsync(issueId);
            if (issue == null)
                return NotFound();

            await _issueRepository.DeleteAsync(issueId);

            _logger.LogWarning($"Issue {issue.Id} deleted by admin");
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
