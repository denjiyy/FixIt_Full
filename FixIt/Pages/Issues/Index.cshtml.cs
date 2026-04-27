using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Services.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.Enums;
using FixIt.Models.AI;

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
    public string Sort { get; set; } = "newest";
    public int SelectedStatus { get; set; } = -1;
    public int SelectedPriority { get; set; } = -1;
    public string SelectedCategory { get; set; } = string.Empty;
    public DateTime? SelectedFromUtc { get; set; }
    public DateTime? SelectedToUtc { get; set; }
    public long FilteredTotal { get; set; }
    public IssuePublicOverview Overview { get; set; } = new();

    public async Task OnGetAsync(int page = 1, string searchQuery = "", int? status = null, int? priority = null, string? category = null, DateTime? from = null, DateTime? to = null, string sort = "newest")
    {
        CurrentPage = Math.Max(1, page);
        SearchQuery = searchQuery ?? "";
        SelectedStatus = status ?? -1;
        SelectedPriority = priority ?? -1;
        SelectedCategory = category ?? string.Empty;
        SelectedFromUtc = from?.Date;
        SelectedToUtc = to?.Date;
        Sort = string.IsNullOrWhiteSpace(sort) ? "newest" : sort.Trim();

        try
        {
            // Parse status enum
            IssueStatus? issueStatus = null;
            if (status.HasValue && status.Value >= 0 && Enum.IsDefined(typeof(IssueStatus), status.Value))
            {
                issueStatus = (IssueStatus)status.Value;
            }

            // Parse priority enum
            IssuePriority? issuePriority = null;
            if (priority.HasValue && priority.Value >= 0 && Enum.IsDefined(typeof(IssuePriority), priority.Value))
            {
                issuePriority = (IssuePriority)priority.Value;
            }

            IssueCategory? issueCategory = null;
            if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<IssueCategory>(category, true, out var parsedCategory))
            {
                issueCategory = parsedCategory;
                SelectedCategory = parsedCategory.ToString();
            }

            var overviewTask = _issueService.GetPublicIssueOverviewAsync();

            // Get issues with filtering
            var result = await _issueService.GetAllIssuesAsync(
                searchQuery: searchQuery,
                status: issueStatus,
                priority: issuePriority,
                category: issueCategory,
                fromUtc: SelectedFromUtc,
                toUtc: SelectedToUtc,
                sort: ParseSortOption(Sort),
                page: CurrentPage,
                pageSize: PageSize
            );

            Issues = result.Items.ToList();
            FilteredTotal = result.Total;
            TotalPages = (int)Math.Ceiling(result.Total / (double)PageSize);
            Overview = await overviewTask;

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
            Overview = new IssuePublicOverview();
        }
    }

    private static IssueSortOption ParseSortOption(string? sort) =>
        sort?.Trim().ToLowerInvariant() switch
        {
            "mostvoted" => IssueSortOption.MostVoted,
            "mostviewed" => IssueSortOption.MostViewed,
            _ => IssueSortOption.Newest
        };
}
