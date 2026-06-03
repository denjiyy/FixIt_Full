using FixIt.Models.Enums;
using FixIt.Models.Issues;
using FixIt.Services.Contracts;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FixIt.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IIssueService _issueService;

    public IndexModel(ILogger<IndexModel> logger, IIssueService issueService)
    {
        _logger = logger;
        _issueService = issueService;
    }

    public IssuePublicOverview Overview { get; private set; } = new();

    /// <summary>
    /// Recently resolved issues, surfaced as community "stories" on the landing page.
    /// </summary>
    public IReadOnlyList<Issue> ResolvedStories { get; private set; } = new List<Issue>();

    public async Task OnGetAsync()
    {
        try
        {
            Overview = await _issueService.GetPublicIssueOverviewAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load homepage issue overview.");
            Overview = new IssuePublicOverview();
        }

        try
        {
            var resolved = await _issueService.GetAllIssuesAsync(
                status: IssueStatus.Fixed,
                sort: IssueSortOption.Newest,
                pageSize: 3);
            ResolvedStories = resolved.Items.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recently resolved issues for the homepage.");
            ResolvedStories = new List<Issue>();
        }
    }
}
