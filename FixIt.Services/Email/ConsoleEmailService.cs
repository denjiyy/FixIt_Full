using Microsoft.Extensions.Logging;

namespace FixIt.Services.Email;

/// <summary>
/// Console-based email service for development (logs emails instead of sending)
/// </summary>
public class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string to, string subject, string htmlContent, string? plainTextContent = null)
    {
        _logger.LogInformation($"[EMAIL] To: {to}");
        _logger.LogInformation($"[EMAIL] Subject: {subject}");
        _logger.LogInformation($"[EMAIL] Body: {(plainTextContent ?? htmlContent).Substring(0, Math.Min(200, (plainTextContent ?? htmlContent).Length))}...");
        return Task.CompletedTask;
    }

    public async Task SendHealthReportEmailAsync(string userEmail, string userName, string cityName, string reportHtml)
    {
        _logger.LogInformation($"[EMAIL] Health Report to {userEmail} for {cityName}");
        await SendEmailAsync(userEmail, $"Weekly Health Report - {cityName}", reportHtml);
    }

    public async Task SendWeeklyReminderEmailAsync(string userEmail, string userName, int openIssuesCount, string cityName)
    {
        _logger.LogInformation($"[EMAIL] Weekly reminder to {userEmail}: {openIssuesCount} open issues in {cityName}");
        await SendEmailAsync(userEmail, $"Weekly Reminder - {openIssuesCount} Open Issues", "Weekly reminder email");
    }

    public async Task SendHazardAlertEmailAsync(string userEmail, string userName, string hazardTitle, string hazardType, double distance)
    {
        _logger.LogInformation($"[EMAIL] Hazard alert to {userEmail}: {hazardType} ({hazardTitle}) at {distance} km");
        await SendEmailAsync(userEmail, $"Safety Alert: {hazardType}", "Hazard alert email");
    }
}
