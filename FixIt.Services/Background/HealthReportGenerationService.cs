using FixIt.Services.Analytics;
using FixIt.Services.Analytics.Models;
using FixIt.Services.Email;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FixIt.Services.Background;

/// <summary>
/// Background service to generate and send health reports on a schedule
/// </summary>
public class HealthReportGenerationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HealthReportGenerationService> _logger;
    private Timer? _weeklyTimer;

    public HealthReportGenerationService(
        IServiceProvider serviceProvider,
        ILogger<HealthReportGenerationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HealthReportGenerationService is starting.");

        // Schedule weekly generation every Sunday at 8 AM UTC
        var now = DateTime.UtcNow;
        var daysUntilSunday = ((7 - (int)now.DayOfWeek) % 7);
        if (daysUntilSunday == 0 && now.Hour >= 8)
            daysUntilSunday = 7;

        var nextReport = now.AddDays(daysUntilSunday).Date.AddHours(8);
        if (nextReport <= now)
            nextReport = nextReport.AddDays(7);

        var weeklyDelay = nextReport - now;
        _logger.LogInformation($"Next health report generation scheduled for: {nextReport:O}");

        _weeklyTimer = new Timer(
            callback: async _ => await GenerateAndSendHealthReportsAsync(),
            state: null,
            dueTime: weeklyDelay,
            period: TimeSpan.FromDays(7));

        return Task.CompletedTask;
    }

    private async Task GenerateAndSendHealthReportsAsync()
    {
        _logger.LogInformation("Starting health report generation and distribution.");

        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var healthReportService = scope.ServiceProvider.GetRequiredService<IHealthReportService>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var cityRepo = scope.ServiceProvider.GetRequiredService<IRepository<FixIt.Models.Locations.City>>();

                // Get all users who have email notifications enabled
                var users = userManager.Users.Where(u => u.EmailNotificationsEnabled).ToList();
                _logger.LogInformation($"Sending health reports to {users.Count} users with notifications enabled.");

                foreach (var user in users)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(user.Email))
                            continue;

                        // Generate report for user's city or global report
                        FixIt.Services.Analytics.Models.HealthReport report;
                        string cityName;

                        if (!string.IsNullOrEmpty(user.PreferredCityId))
                        {
                            var city = await cityRepo.GetByIdAsync(user.PreferredCityId);
                            report = await healthReportService.GetCityHealthReportAsync(user.PreferredCityId);
                            cityName = city?.Name ?? "Your City";
                        }
                        else
                        {
                            report = await healthReportService.GetGlobalHealthReportAsync();
                            cityName = "Global";
                        }

                        // Generate HTML report
                        var reportHtml = GenerateHealthReportHtml(report);

                        // Send email
                        await emailService.SendHealthReportEmailAsync(
                            user.Email,
                            user.UserName ?? "User",
                            cityName,
                            reportHtml);

                        _logger.LogInformation($"Health report email sent to {user.Email}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send health report to user {user.Email}");
                    }
                }

                _logger.LogInformation("Health report generation completed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health report generation");
        }
    }

    private string GenerateHealthReportHtml(FixIt.Services.Analytics.Models.HealthReport report)
    {
        var topIssuesHtml = "";
        if (report.TopIssues?.Count > 0)
        {
            topIssuesHtml = @"<div style=""background: white; padding: 15px; border-radius: 8px; border: 1px solid #ddd; margin-top: 20px;"">
                <h4 style=""margin-top: 0;"">TOP ISSUES</h4>
                <ul style=""margin: 0; padding-left: 20px; color: #333;"">";
            
            foreach (var issue in report.TopIssues.Take(3))
            {
                topIssuesHtml += $@"
                    <li style=""margin-bottom: 8px;"">
                        <strong>{System.Net.WebUtility.HtmlEncode(issue.Title)}</strong><br/>
                        <small style=""color: #666;"">{issue.Upvotes} upvotes, {issue.Comments} comments</small>
                    </li>";
            }
            
            topIssuesHtml += @"
                </ul>
            </div>";
        }

        return $@"
<div style=""margin: 20px 0;"">
    <div style=""display: grid; grid-template-columns: 1fr 1fr; gap: 15px; margin-bottom: 20px;"">
        <div style=""background: #f0f8ff; padding: 15px; border-radius: 8px; border-left: 4px solid #0d6efd;"">
            <div style=""font-size: 12px; color: #666; margin-bottom: 5px;"">HEALTH SCORE</div>
            <div style=""font-size: 2rem; font-weight: bold; color: #0d6efd;"">{report.HealthScore:F0} / 100</div>
        </div>
        <div style=""background: #f0fff4; padding: 15px; border-radius: 8px; border-left: 4px solid #198754;"">
            <div style=""font-size: 12px; color: #666; margin-bottom: 5px;"">RESOLUTION RATE</div>
            <div style=""font-size: 2rem; font-weight: bold; color: #198754;"">{report.ResolutionRate:F1}%</div>
        </div>
    </div>

    <div style=""display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 15px; margin-bottom: 20px;"">
        <div style=""background: white; padding: 15px; border-radius: 8px; border: 1px solid #ddd; text-align: center;"">
            <div style=""font-size: 12px; color: #666; margin-bottom: 5px;"">TOTAL ISSUES</div>
            <div style=""font-size: 1.5rem; font-weight: bold;"">{report.TotalIssues}</div>
        </div>
        <div style=""background: white; padding: 15px; border-radius: 8px; border: 1px solid #ddd; text-align: center;"">
            <div style=""font-size: 12px; color: #666; margin-bottom: 5px;"">OPEN ISSUES</div>
            <div style=""font-size: 1.5rem; font-weight: bold; color: #dc3545;"">{report.OpenIssues}</div>
        </div>
        <div style=""background: white; padding: 15px; border-radius: 8px; border: 1px solid #ddd; text-align: center;"">
            <div style=""font-size: 12px; color: #666; margin-bottom: 5px;"">RESOLVED</div>
            <div style=""font-size: 1.5rem; font-weight: bold; color: #198754;"">{report.ResolvedIssues}</div>
        </div>
    </div>

    <div style=""background: white; padding: 15px; border-radius: 8px; border: 1px solid #ddd; margin-bottom: 20px;"">
        <h4 style=""margin-top: 0;"">PRIORITY BREAKDOWN</h4>
        <div style=""display: grid; grid-template-columns: repeat(4, 1fr); gap: 10px; text-align: center;"">
            <div>
                <div style=""font-size: 12px; color: #666; margin-bottom: 5px;"">CRITICAL</div>
                <div style=""font-size: 1.5rem; font-weight: bold; color: #dc3545;"">{report.CriticalIssues}</div>
            </div>
            <div>
                <div style=""font-size: 12px; color: #666; margin-bottom: 5px;"">HIGH</div>
                <div style=""font-size: 1.5rem; font-weight: bold; color: #fd7e14;"">{report.HighIssues}</div>
            </div>
            <div>
                <div style=""font-size: 12px; color: #666; margin-bottom: 5px;"">MEDIUM</div>
                <div style=""font-size: 1.5rem; font-weight: bold; color: #ffc107;"">{report.MediumIssues}</div>
            </div>
            <div>
                <div style=""font-size: 12px; color: #666; margin-bottom: 5px;"">LOW</div>
                <div style=""font-size: 1.5rem; font-weight: bold; color: #198754;"">{report.LowIssues}</div>
            </div>
        </div>
    </div>

    <div style=""background: white; padding: 15px; border-radius: 8px; border: 1px solid #ddd;"">
        <h4 style=""margin-top: 0;"">TIME METRICS</h4>
        <div style=""display: grid; grid-template-columns: 1fr 1fr; gap: 10px;"">
            <div>
                <div style=""font-size: 12px; color: #666; margin-bottom: 5px;"">AVG RESOLUTION TIME</div>
                <div style=""font-size: 1.2rem; font-weight: bold;"">{report.AverageResolutionTimeHours:F1}h</div>
            </div>
            <div>
                <div style=""font-size: 12px; color: #666; margin-bottom: 5px;"">AVG RESPONSE TIME</div>
                <div style=""font-size: 1.2rem; font-weight: bold;"">{report.AverageResponseTimeHours:F1}h</div>
            </div>
        </div>
    </div>

    {topIssuesHtml}
</div>";
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HealthReportGenerationService is stopping.");
        _weeklyTimer?.Dispose();
        await base.StopAsync(stoppingToken);
    }

    public override void Dispose()
    {
        _weeklyTimer?.Dispose();
        base.Dispose();
    }
}
