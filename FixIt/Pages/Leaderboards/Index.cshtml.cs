using FixIt.Models.Gamification;
using FixIt.Services.Gamification;
using FixIt.Data.Repository.Contracts;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace FixIt.Pages.Leaderboards;

public class IndexModel : PageModel
{
    private readonly IReputationService _reputationService;
    private readonly IRepository<LeaderboardEntry> _leaderboardRepository;
    private readonly ILogger<IndexModel> _logger;

    public List<LeaderboardEntry> WeeklyLeaderboard { get; set; } = new();
    public List<LeaderboardEntry> MonthlyLeaderboard { get; set; } = new();
    public List<LeaderboardEntry> AllTimeLeaderboard { get; set; } = new();

    public IndexModel(
        IReputationService reputationService,
        IRepository<LeaderboardEntry> leaderboardRepository,
        ILogger<IndexModel> logger)
    {
        _reputationService = reputationService;
        _leaderboardRepository = leaderboardRepository;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        // Load all three leaderboards
        WeeklyLeaderboard = await _reputationService.GetLeaderboardAsync(LeaderboardPeriod.Weekly, 50);
        MonthlyLeaderboard = await _reputationService.GetLeaderboardAsync(LeaderboardPeriod.Monthly, 50);
        AllTimeLeaderboard = await _reputationService.GetLeaderboardAsync(LeaderboardPeriod.AllTime, 50);

        // Regenerate leaderboards if they're empty or stale (fallback mechanism)
        await RegenerateIfNeededAsync();

        // Reload leaderboards if they were regenerated
        if (WeeklyLeaderboard.Count == 0 || MonthlyLeaderboard.Count == 0 || AllTimeLeaderboard.Count == 0)
        {
            WeeklyLeaderboard = await _reputationService.GetLeaderboardAsync(LeaderboardPeriod.Weekly, 50);
            MonthlyLeaderboard = await _reputationService.GetLeaderboardAsync(LeaderboardPeriod.Monthly, 50);
            AllTimeLeaderboard = await _reputationService.GetLeaderboardAsync(LeaderboardPeriod.AllTime, 50);
        }
    }

    /// <summary>
    /// Regenerates leaderboards if they're empty or haven't been updated in the last hour
    /// </summary>
    private async Task RegenerateIfNeededAsync()
    {
        try
        {
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            
            // Check each leaderboard period
            var periods = new[] { LeaderboardPeriod.Weekly, LeaderboardPeriod.Monthly, LeaderboardPeriod.AllTime };

            foreach (var period in periods)
            {
                var entries = await _leaderboardRepository.FindAsync(e => e.Period == period);
                var entryList = entries.ToList();

                // Regenerate if empty or stale (older than 1 hour)
                if (entryList.Count == 0 || (entryList.FirstOrDefault()?.CreatedAt < oneHourAgo))
                {
                    _logger.LogInformation($"Regenerating {period} leaderboard (empty or stale)");
                    
                    if (period == LeaderboardPeriod.Weekly)
                        await _reputationService.RegenerateWeeklyLeaderboardAsync();
                    else if (period == LeaderboardPeriod.Monthly)
                        await _reputationService.RegenerateMonthlyLeaderboardAsync();
                    else if (period == LeaderboardPeriod.AllTime)
                        await _reputationService.RegenerateAllTimeLeaderboardAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating leaderboards on-demand");
        }
    }
}
