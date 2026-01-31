using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Services.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.AI;
using FixIt.Services.AI;

namespace FixIt.Pages.Issues;

public class IssueDetailModel : PageModel
{
    private readonly IIssueService _issueService;
    private readonly IIssueAnalysisService _analysisService;

    public IssueDetailModel(IIssueService issueService, IIssueAnalysisService analysisService)
    {
        _issueService = issueService;
        _analysisService = analysisService;
    }

    public Issue? Issue { get; set; }
    public IssueAnalysis? Analysis { get; set; }
    public List<dynamic>? Comments { get; set; } = new();
    public int? IssueCount { get; set; }
    
    public List<dynamic> SortedStatusHistory
    {
        get
        {
            if (Issue?.StatusHistory == null) return new List<dynamic>();
            
            var statusHistory = Issue.StatusHistory as IEnumerable<dynamic> ?? new List<dynamic>();
            return statusHistory.OrderByDescending(h => ((dynamic)h).ChangedAt).ToList();
        }
    }

    public async Task OnGetAsync(string id)
    {
        try
        {
            // Load issue from service
            Issue = await _issueService.GetIssueByIdAsync(id);
            
            if (Issue == null)
            {
                ModelState.AddModelError("", "Issue not found");
            }
            else
            {
                // Load AI analysis if available
                Analysis = await _analysisService.GetAnalysisAsync(id);
            }
            
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
