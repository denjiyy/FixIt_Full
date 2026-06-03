using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Models.Enums;
using FixIt.Models.Issues;
using FixIt.Models.Users;
using FixIt.Services.Contracts;

namespace FixIt.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IIssueService _issueService;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ILogger<IndexModel> logger, IIssueService issueService, UserManager<ApplicationUser> userManager)
    {
        _logger = logger;
        _issueService = issueService;
        _userManager = userManager;
    }

    public IssuePublicOverview Overview { get; private set; } = new();

    /// <summary>
    /// Recently resolved issues, surfaced as community "stories" on the landing page.
    /// </summary>
    public IReadOnlyList<Issue> ResolvedStories { get; private set; } = new List<Issue>();

    /// <summary>True when an authenticated resident should see the "your town" home.</summary>
    public bool IsResident { get; private set; }

    /// <summary>The signed-in resident's display name (for the greeting).</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>The signed-in resident's most recent reports.</summary>
    public IReadOnlyList<Issue> MyReports { get; private set; } = new List<Issue>();

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

        if (User?.Identity?.IsAuthenticated == true)
        {
            IsResident = true;
            try
            {
                var appUser = await _userManager.GetUserAsync(User);
                DisplayName = appUser?.DisplayName
                    ?? User.Identity?.Name
                    ?? string.Empty;

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    var mine = await _issueService.GetUserIssuesAsync(userId, 1, 3);
                    MyReports = mine.Items.ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load resident home data for the homepage.");
                MyReports = new List<Issue>();
            }
        }
    }
}
