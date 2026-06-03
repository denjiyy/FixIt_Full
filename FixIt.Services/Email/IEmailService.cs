namespace FixIt.Services.Email;

/// <summary>
/// Email service for sending transactional emails
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send a plain text or HTML email
    /// </summary>
    Task SendEmailAsync(string to, string subject, string htmlContent, string? plainTextContent = null);

    /// <summary>
    /// Send health report email to a user
    /// </summary>
    Task SendHealthReportEmailAsync(string userEmail, string userName, string cityName, string reportHtml);
}
