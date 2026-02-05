using FixIt.Services.Gamification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FixIt.Services.Background;

/// <summary>
/// Background service to regenerate leaderboards on a schedule
/// </summary>
public class LeaderboardRegenerationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LeaderboardRegenerationService> _logger;
    private Timer? _weeklyTimer;
    private Timer? _monthlyTimer;

    public LeaderboardRegenerationService(
        IServiceProvider serviceProvider,
        ILogger<LeaderboardRegenerationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LeaderboardRegenerationService is starting.");

        // Schedule weekly regeneration every Monday at 2 AM UTC
        var now = DateTime.UtcNow;
        var nextMonday = now.AddDays(((7 - (int)now.DayOfWeek) % 7));
        nextMonday = nextMonday.Date.AddHours(2);
        if (nextMonday <= now)
            nextMonday = nextMonday.AddDays(7);

        var weeklyDelay = nextMonday - now;
        _logger.LogInformation($"Next weekly leaderboard regeneration scheduled for: {nextMonday:O}");

        _weeklyTimer = new Timer(
            callback: async _ => await RegenerateWeeklyLeaderboardAsync(),
            state: null,
            dueTime: weeklyDelay,
            period: TimeSpan.FromDays(7));

        // Schedule monthly regeneration on the 1st of each month at 2 AM UTC
        var nextMonth = now.Month == 12 
            ? new DateTime(now.Year + 1, 1, 1, 2, 0, 0)
            : new DateTime(now.Year, now.Month + 1, 1, 2, 0, 0);

        if (nextMonth <= now)
            nextMonth = nextMonth.AddMonths(1);

        var monthlyDelay = nextMonth - now;
        _logger.LogInformation($"Next monthly leaderboard regeneration scheduled for: {nextMonth:O}");

        _monthlyTimer = new Timer(
            callback: async _ => await RegenerateMonthlyLeaderboardAsync(),
            state: null,
            dueTime: monthlyDelay,
            period: TimeSpan.FromDays(30));

        return Task.CompletedTask;
    }

    private async Task RegenerateWeeklyLeaderboardAsync()
    {
        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var reputationService = scope.ServiceProvider.GetRequiredService<IReputationService>();
                await reputationService.RegenerateWeeklyLeaderboardAsync();
                _logger.LogInformation("Weekly leaderboard regenerated successfully.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating weekly leaderboard");
        }
    }

    private async Task RegenerateMonthlyLeaderboardAsync()
    {
        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var reputationService = scope.ServiceProvider.GetRequiredService<IReputationService>();
                await reputationService.RegenerateMonthlyLeaderboardAsync();
                _logger.LogInformation("Monthly leaderboard regenerated successfully.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating monthly leaderboard");
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LeaderboardRegenerationService is stopping.");

        _weeklyTimer?.Dispose();
        _monthlyTimer?.Dispose();

        await base.StopAsync(stoppingToken);
    }

    public override void Dispose()
    {
        _weeklyTimer?.Dispose();
        _monthlyTimer?.Dispose();
        base.Dispose();
    }
}
