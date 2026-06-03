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
        var content = plainTextContent ?? htmlContent;
        var previewLength = Math.Min(200, content.Length);
        var bodyPreview = content.Substring(0, previewLength);

        _logger.LogInformation("[EMAIL] To: {RecipientEmail}", to);
        _logger.LogInformation("[EMAIL] Subject: {EmailSubject}", subject);
        _logger.LogInformation("[EMAIL] Body: {BodyPreview}...", bodyPreview);
        return Task.CompletedTask;
    }

    public async Task SendHealthReportEmailAsync(string userEmail, string userName, string cityName, string reportHtml)
    {
        _logger.LogInformation("[EMAIL] Health Report to {RecipientEmail} for {CityName}", userEmail, cityName);
        await SendEmailAsync(userEmail, $"Weekly Health Report - {cityName}", reportHtml);
    }
}
