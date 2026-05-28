using Cronos;
using FixIt.Services.Gamification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FixIt.Services.Background;

/// <summary>
/// Recomputes leaderboards on a schedule. Uses cron expressions evaluated via
/// Cronos so we don't drift on month boundaries (the previous Timer-based
/// implementation used TimeSpan.FromDays(30) for "monthly", drifting ~5 days
/// per year) and so DST transitions are handled correctly.
///
/// Schedule re-anchors per iteration: each task sleeps until its next cron
/// occurrence and then loops. Process restarts cause at most a single missed
/// iteration; for leaderboards (which are recomputed from durable source data
/// anyway), this is acceptable.
/// </summary>
public class LeaderboardRegenerationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LeaderboardRegenerationService> _logger;

    // 02:00 UTC each Monday.
    private static readonly CronExpression WeeklyCron = CronExpression.Parse("0 2 * * 1");
    // 02:00 UTC on the 1st of every month.
    private static readonly CronExpression MonthlyCron = CronExpression.Parse("0 2 1 * *");
    // 03:00 UTC every day.
    private static readonly CronExpression AllTimeCron = CronExpression.Parse("0 3 * * *");

    public LeaderboardRegenerationService(
        IServiceProvider serviceProvider,
        ILogger<LeaderboardRegenerationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LeaderboardRegenerationService starting (cron-driven).");

        // Three independent loops, one per cadence. Failures in one don't
        // affect the others.
        var weekly = RunOnCronAsync(WeeklyCron, "weekly", RegenerateWeeklyAsync, stoppingToken);
        var monthly = RunOnCronAsync(MonthlyCron, "monthly", RegenerateMonthlyAsync, stoppingToken);
        var allTime = RunOnCronAsync(AllTimeCron, "all-time", RegenerateAllTimeAsync, stoppingToken);

        return Task.WhenAll(weekly, monthly, allTime);
    }

    private async Task RunOnCronAsync(
        CronExpression cron,
        string label,
        Func<CancellationToken, Task> work,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var next = cron.GetNextOccurrence(now, TimeZoneInfo.Utc);
            if (next == null)
            {
                _logger.LogWarning("Cron expression for {Label} produced no future occurrence; stopping.", label);
                return;
            }

            var delay = next.Value - now;
            _logger.LogInformation(
                "Next {Label} leaderboard regeneration scheduled for {NextRun:O}",
                label, next.Value);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            try
            {
                await work(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error regenerating {Label} leaderboard", label);
            }
        }
    }

    private async Task RegenerateWeeklyAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReputationService>();
        await service.RegenerateWeeklyLeaderboardAsync();
        _logger.LogInformation("Weekly leaderboard regenerated.");
    }

    private async Task RegenerateMonthlyAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReputationService>();
        await service.RegenerateMonthlyLeaderboardAsync();
        _logger.LogInformation("Monthly leaderboard regenerated.");
    }

    private async Task RegenerateAllTimeAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReputationService>();
        await service.RegenerateAllTimeLeaderboardAsync();
        _logger.LogInformation("All-time leaderboard regenerated.");
    }
}
