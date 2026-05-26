using FixIt.Models.Issues;
using FixIt.Services.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FixIt.Pages;

public class SearchModel : PageModel
{
    private readonly IIssueService _issueService;
    private readonly ILogger<SearchModel> _logger;

    public SearchModel(IIssueService issueService, ILogger<SearchModel> logger)
    {
        _issueService = issueService;
        _logger = logger;
    }

    public List<Issue> Issues { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalPages { get; set; } = 1;
    public long TotalCount { get; set; }
    public string Query { get; set; } = string.Empty;

    public async Task OnGetAsync([FromQuery(Name = "q")] string? q = null, [FromQuery(Name = "page")] int pageNumber = 1)
    {
        Query = (q ?? string.Empty).Trim();
        CurrentPage = Math.Max(1, pageNumber);

        if (string.IsNullOrWhiteSpace(Query))
        {
            Issues = new List<Issue>();
            TotalCount = 0;
            TotalPages = 1;
            return;
        }

        try
        {
            var result = await _issueService.GetAllIssuesAsync(
                searchQuery: Query,
                page: CurrentPage,
                pageSize: PageSize);

            Issues = result.Items.ToList();
            TotalCount = result.Total;
            TotalPages = Math.Max(1, (int)Math.Ceiling(result.Total / (double)PageSize));

            if (CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for query {Query} page {Page}", Query, CurrentPage);
            Issues = new List<Issue>();
            TotalCount = 0;
            TotalPages = 1;
        }
    }
}
