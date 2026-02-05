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
    private readonly IMediaService _mediaService;
    private readonly IIssueAnalysisService _analysisService;

    public IssueDetailModel(
        IIssueService issueService, 
        IMediaService mediaService,
        IIssueAnalysisService analysisService)
    {
        _issueService = issueService;
        _mediaService = mediaService;
        _analysisService = analysisService;
    }

    public Issue? Issue { get; set; }
    public IssueAnalysis? Analysis { get; set; }
    public List<FixIt.Models.Media.Media> MediaList { get; set; } = new();
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
            // Load issue from service
            Issue = await _issueService.GetIssueByIdAsync(Id);
            
            if (Issue == null)
            {
                return NotFound();
            }

            // Load AI analysis if available, otherwise trigger it
            Analysis = await _analysisService.GetAnalysisAsync(Id);
            
            // If no analysis exists, trigger it in the background
            if (Analysis == null)
            {
                try
                {
                    // Fire and forget - don't wait for analysis to complete
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _analysisService.AnalyzeIssueAsync(Id);
                        }
                        catch
                        {
                            // Silently fail - analysis will be retried on next page load
                        }
                    });
                }
                catch
                {
                    // Analysis trigger failed, but page still loads
                }
            }
            
            // Load media files
            MediaList = await _mediaService.GetMediaForReferenceAsync(
                FixIt.Models.Enums.MediaReferenceType.Issue, 
                Id
            );
            
            Comments = new List<dynamic>();
            
            return Page();
        }
        catch (Exception ex)
        {
            return NotFound();
        }
    }

    public async Task<IActionResult> OnPostAddCommentAsync(string commentText)
    {
        if (string.IsNullOrWhiteSpace(commentText))
        {
            ModelState.AddModelError("", "Comment cannot be empty");
            return Page();
        }

        try
        {
            // TODO: Add comment via service
            return RedirectToPage(new { id = Id });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Failed to add comment");
            return Page();
        }
    }
}