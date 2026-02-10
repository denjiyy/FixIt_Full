using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using FixIt.Services.Contracts;
using FixIt.Models.Issues;
using System.Security.Claims;

namespace FixIt.Pages.Issues;

[Authorize]
public class DeleteIssueModel : PageModel
{
    private readonly IIssueService _issueService;
    private readonly ILogger<DeleteIssueModel> _logger;

    public DeleteIssueModel(
        IIssueService issueService,
        ILogger<DeleteIssueModel> logger)
    {
        _issueService = issueService;
        _logger = logger;
    }

    public Issue? Issue { get; set; }

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

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading issue for deletion {IssueId}", Id);
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(Id))
        {
            return NotFound();
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

            // Delete the issue (soft delete)
            await _issueService.DeleteIssueAsync(Id);

            _logger.LogInformation("Issue {IssueId} deleted by user {UserId}", Id, userId);

            TempData["SuccessMessage"] = "Issue has been deleted successfully";
            return RedirectToPage("./Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting issue {IssueId}", Id);
            ModelState.AddModelError("", "Failed to delete issue");
            return Page();
        }
    }
}
