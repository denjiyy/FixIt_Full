using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace FixIt.Services.Email;

/// <summary>
/// SMTP-based email service for sending transactional emails
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly SmtpClient _smtpClient;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var smtpConfig = _configuration.GetSection("Email:Smtp");
        var host = smtpConfig["Host"];
        var port = int.TryParse(smtpConfig["Port"], out var p) ? p : 587;
        var username = smtpConfig["Username"];
        var password = smtpConfig["Password"];
        var enableSsl = bool.TryParse(smtpConfig["EnableSsl"], out var ssl) && ssl;

        _fromEmail = _configuration["Email:FromAddress"] ?? "noreply@fixit.local";
        _fromName = _configuration["Email:FromName"] ?? "FixIt";

        _smtpClient = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrEmpty(username))
        {
            _smtpClient.Credentials = new NetworkCredential(username, password);
        }
    }

    public async Task SendEmailAsync(string to, string subject, string htmlContent, string? plainTextContent = null)
    {
        try
        {
            using var mailMessage = new MailMessage(_fromEmail, to)
            {
                Subject = subject,
                IsBodyHtml = !string.IsNullOrEmpty(htmlContent),
                Body = htmlContent ?? plainTextContent ?? ""
            };

            if (!string.IsNullOrEmpty(plainTextContent) && !string.IsNullOrEmpty(htmlContent))
            {
                mailMessage.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                    plainTextContent, null, "text/plain"));
                mailMessage.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                    htmlContent, null, "text/html"));
            }

            mailMessage.From = new MailAddress(_fromEmail, _fromName);

            await _smtpClient.SendMailAsync(mailMessage);
            _logger.LogInformation($"Email sent successfully to {to}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send email to {to}: {ex.Message}");
            throw;
        }
    }

    public async Task SendHealthReportEmailAsync(string userEmail, string userName, string cityName, string reportHtml)
    {
        var subject = $"Weekly Health Report - {cityName}";
        var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 8px; margin-bottom: 20px; }}
        .header h1 {{ margin: 0; }}
        .content {{ background: #f5f5f5; padding: 20px; border-radius: 8px; margin-bottom: 20px; }}
        .footer {{ text-align: center; color: #999; font-size: 12px; }}
        .button {{ display: inline-block; background: #667eea; color: white; padding: 10px 20px; text-decoration: none; border-radius: 4px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Community Health Report</h1>
            <p>{cityName} - {DateTime.UtcNow:MMMM d, yyyy}</p>
        </div>
        <div class=""content"">
            <p>Hi {userName},</p>
            <p>Here's your weekly community health report for {cityName}:</p>
            {reportHtml}
            <p style=""margin-top: 20px;"">
                <a href=""{_configuration["App:BaseUrl"]}/HealthReports"" class=""button"">View Full Report</a>
            </p>
        </div>
        <div class=""footer"">
            <p>© 2026 FixIt. All rights reserved.</p>
            <p><a href=""{_configuration["App:BaseUrl"]}/settings/email-preferences"" style=""color: #667eea; text-decoration: none;"">Manage Email Preferences</a></p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(userEmail, subject, htmlContent);
    }

    public async Task SendWeeklyReminderEmailAsync(string userEmail, string userName, int openIssuesCount, string cityName)
    {
        var subject = $"Weekly Reminder - {openIssuesCount} Open Issues in {cityName}";
        var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 8px; margin-bottom: 20px; }}
        .stats {{ background: white; border-left: 4px solid #667eea; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .stats-number {{ font-size: 2rem; font-weight: bold; color: #667eea; }}
        .button {{ display: inline-block; background: #667eea; color: white; padding: 10px 20px; text-decoration: none; border-radius: 4px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Weekly community reminder</h1>
        </div>
        <p>Hi {userName},</p>
        <p>There are currently <strong>{openIssuesCount} open issues</strong> in {cityName} that need attention.</p>
        <div class=""stats"">
            <div class=""stats-number"">{openIssuesCount}</div>
            <div>Open Issues</div>
        </div>
        <p>Help us improve {cityName} by responding to these issues!</p>
        <p style=""margin-top: 20px;"">
            <a href=""{_configuration["App:BaseUrl"]}/cities/{cityName}"" class=""button"">View Issues</a>
        </p>
        <p>Thank you for being part of the FixIt community!</p>
    </div>
</body>
</html>";

        await SendEmailAsync(userEmail, subject, htmlContent);
    }

    public async Task SendHazardAlertEmailAsync(string userEmail, string userName, string hazardTitle, string hazardType, double distance)
    {
        var subject = $"Safety Alert: {hazardType} Detected Nearby";
        var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .alert {{ background: #fff3cd; border-left: 4px solid #ffc107; padding: 20px; border-radius: 8px; margin: 20px 0; }}
        .button {{ display: inline-block; background: #ffc107; color: black; padding: 10px 20px; text-decoration: none; border-radius: 4px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <h2>Safety Alert</h2>
        <div class=""alert"">
            <h3>⚠️ {hazardType} Detected</h3>
            <p><strong>{hazardTitle}</strong></p>
            <p>A new {hazardType.ToLower()} has been reported approximately {distance:.1f} km from your location.</p>
        </div>
        <p>Stay safe and stay aware of your surroundings!</p>
        <p style=""margin-top: 20px;"">
            <a href=""{_configuration["App:BaseUrl"]}/safety/hazard-mode"" class=""button"">View on Hazard Map</a>
        </p>
    </div>
</body>
</html>";

        await SendEmailAsync(userEmail, subject, htmlContent);
    }
}
