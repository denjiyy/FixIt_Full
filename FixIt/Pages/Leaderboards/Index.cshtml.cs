using FixIt.Models.Gamification;
using FixIt.Services.Gamification;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FixIt.Pages.Leaderboards;

public class IndexModel : PageModel
{
    private readonly IReputationService _reputationService;

    public List<LeaderboardEntry> WeeklyLeaderboard { get; set; } = new();
    public List<LeaderboardEntry> MonthlyLeaderboard { get; set; } = new();
    public List<LeaderboardEntry> AllTimeLeaderboard { get; set; } = new();

    public IndexModel(IReputationService reputationService)
    {
        _reputationService = reputationService;
    }

    public async Task OnGetAsync()
    {
        // Load all three leaderboards
        WeeklyLeaderboard = await _reputationService.GetLeaderboardAsync(LeaderboardPeriod.Weekly, 50);
        MonthlyLeaderboard = await _reputationService.GetLeaderboardAsync(LeaderboardPeriod.Monthly, 50);
        AllTimeLeaderboard = await _reputationService.GetLeaderboardAsync(LeaderboardPeriod.AllTime, 50);
    }
}
