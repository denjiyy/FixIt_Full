using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Services.Contracts;

namespace FixIt.Pages.Issues;

public class IssueDetailModel : PageModel
{
    private readonly IIssueService _issueService;

    public IssueDetailModel(IIssueService issueService)
    {
        _issueService = issueService;
    }

    public dynamic? Issue { get; set; }
    public List<dynamic>? Comments { get; set; } = new();
    public int? IssueCount { get; set; }
    
    public List<dynamic> SortedStatusHistory
    {
        get
        {
            var issue = Issue as dynamic;
            if (issue?.StatusHistory == null) return new List<dynamic>();
            
            var statusHistory = issue.StatusHistory as IEnumerable<dynamic> ?? new List<dynamic>();
            return statusHistory.OrderByDescending(h => ((dynamic)h).ChangedAt).ToList();
        }
    }

    public async Task OnGetAsync(string id)
    {
        try
        {
            // TODO: Load issue and comments from service
            Comments = new List<dynamic>();
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Failed to load issue");
        }
    }

    public async Task<IActionResult> OnPostAddCommentAsync(string id, string commentText)
    {
        if (string.IsNullOrWhiteSpace(commentText))
        {
            ModelState.AddModelError("", "Comment cannot be empty");
            return Page();
        }

        try
        {
            // TODO: Add comment via service
            return RedirectToPage(new { id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Failed to add comment");
            return Page();
        }
    }
}
