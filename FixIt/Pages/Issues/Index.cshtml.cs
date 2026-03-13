using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Services.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.Enums;

namespace FixIt.Pages.Issues;

public class IssueListModel : PageModel
{
    private readonly IIssueService _issueService;
    private readonly ILogger<IssueListModel> _logger;

    public IssueListModel(IIssueService issueService, ILogger<IssueListModel> logger)
    {
        _issueService = issueService;
        _logger = logger;
    }

    public List<Issue> Issues { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalPages { get; set; } = 1;
    public string SearchQuery { get; set; } = "";
    public int SelectedStatus { get; set; } = -1;
    public int SelectedPriority { get; set; } = -1;

    public async Task OnGetAsync(int page = 1, string searchQuery = "", int status = -1, int priority = -1)
    {
        CurrentPage = Math.Max(1, page);
        SearchQuery = searchQuery ?? "";
        SelectedStatus = status;
        SelectedPriority = priority;

        try
        {
            // Parse status enum
            IssueStatus? issueStatus = null;
            if (status >= 0 && Enum.IsDefined(typeof(IssueStatus), status))
            {
                issueStatus = (IssueStatus)status;
            }

            // Parse priority enum
            IssuePriority? issuePriority = null;
            if (priority >= 0 && Enum.IsDefined(typeof(IssuePriority), priority))
            {
                issuePriority = (IssuePriority)priority;
            }

            // Get issues with filtering
            var result = await _issueService.GetAllIssuesAsync(
                searchQuery: searchQuery,
                status: issueStatus,
                priority: issuePriority,
                page: CurrentPage,
                pageSize: PageSize
            );

            Issues = result.Items.ToList();
            TotalPages = (int)Math.Ceiling(result.Total / (double)PageSize);

            // Validate page number
            if (CurrentPage > TotalPages && TotalPages > 0)
            {
                CurrentPage = TotalPages;
            }
        }
        catch (Exception ex)
        {
            // Log error
            _logger.LogError(ex, "Error loading issues for page {CurrentPage}", CurrentPage);
            ModelState.AddModelError("", "Failed to load issues. Please try again.");
            Issues = new List<Issue>();
            TotalPages = 1;
        }
    }
}
