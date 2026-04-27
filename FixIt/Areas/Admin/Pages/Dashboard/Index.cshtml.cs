using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using FixIt.Models.Users;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.Moderation;
using FixIt.Models.Enums;
using FixIt.Services.Constants;

namespace FixIt.Areas.Admin.Pages.Dashboard;

[Authorize(Policy = PolicyNames.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRepository<Issue> _issueRepository;
    private readonly IRepository<ContentReport> _reportRepository;
    private readonly ILogger<IndexModel> _logger;

    public int TotalUsers { get; set; }
    public int TotalIssues { get; set; }
    public int TotalReports { get; set; }
    public int ResolvedIssues { get; set; }
    public int PendingReports { get; set; }
    public int ActiveModerators { get; set; }

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        IRepository<Issue> issueRepository,
        IRepository<ContentReport> reportRepository,
        ILogger<IndexModel> logger)
    {
        _userManager = userManager;
        _issueRepository = issueRepository;
        _reportRepository = reportRepository;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            // Get total users
            TotalUsers = _userManager.Users.Count();
            ActiveModerators = _userManager.Users.Count(u => u.Role == UserRole.Moderator);

            // Get issue statistics
            TotalIssues = (int)await _issueRepository.CountAsync();
            ResolvedIssues = (int)await _issueRepository.CountAsync(i => i.Status == IssueStatus.Fixed);

            // Get report statistics
            TotalReports = (int)await _reportRepository.CountAsync();
            PendingReports = (int)await _reportRepository.CountAsync(r => r.Status == ReportStatus.Pending);

            _logger.LogInformation($"Admin dashboard loaded for user {User?.Identity?.Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading admin dashboard: {ex.Message}");
        }
    }
}
