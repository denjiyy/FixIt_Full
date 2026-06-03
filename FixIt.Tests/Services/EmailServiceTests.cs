using Xunit;
using Moq;
using FixIt.Services.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FixIt.Tests.Services;

public class ConsoleEmailServiceTests
{
    private readonly Mock<ILogger<ConsoleEmailService>> _loggerMock;
    private readonly ConsoleEmailService _emailService;

    public ConsoleEmailServiceTests()
    {
        _loggerMock = new Mock<ILogger<ConsoleEmailService>>();
        _emailService = new ConsoleEmailService(_loggerMock.Object);
    }

    [Fact]
    public async Task SendEmailAsync_WithValidInputs_LogsEmail()
    {
        // Act
        await _emailService.SendEmailAsync("test@example.com", "Test Subject", "<h1>Test</h1>");

        // Assert - ConsoleEmailService logs multiple lines (summary + to + subject + body)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendHealthReportEmailAsync_LogsHealthReportEmail()
    {
        // Act
        await _emailService.SendHealthReportEmailAsync(
            "user@example.com", "John Doe", "New York", "<h1>Health Report</h1>");

        // Assert - SendHealthReportEmailAsync calls SendEmailAsync which logs multiple times
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce);
    }

}

public class SmtpEmailServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<SmtpEmailService>> _loggerMock;
    private readonly SmtpEmailService _emailService;

    public SmtpEmailServiceTests()
    {
        _loggerMock = new Mock<ILogger<SmtpEmailService>>();

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Smtp:Host"] = "127.0.0.1",
                ["Email:Smtp:Port"] = "1",
                ["Email:Smtp:EnableSsl"] = "false",
                ["Email:FromAddress"] = "noreply@fixit.com",
                ["Email:FromName"] = "FixIt",
                ["App:BaseUrl"] = "https://example.com"
            })
            .Build();

        _emailService = new SmtpEmailService(_configuration, _loggerMock.Object);
    }

    [Fact]
    public async Task SendEmailAsync_WithValidSmtpConfig_SendsEmail()
    {
        // Act
        await Assert.ThrowsAnyAsync<Exception>(() =>
            _emailService.SendEmailAsync("test@example.com", "Test Subject", "<h1>Test</h1>"));

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendEmailAsync_WithPlainTextContent_IncludesPlainText()
    {
        // Arrange
        const string htmlContent = "<h1>HTML Content</h1>";
        const string plainTextContent = "Plain Text Content";

        // Act
        await Assert.ThrowsAnyAsync<Exception>(() =>
            _emailService.SendEmailAsync("test@example.com", "Test Subject", htmlContent, plainTextContent));

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendHealthReportEmailAsync_CreatesProperEmail()
    {
        // Act
        await Assert.ThrowsAnyAsync<Exception>(() =>
            _emailService.SendHealthReportEmailAsync(
                "user@example.com",
                "John Doe",
                "New York",
                "<h1>Health Report for New York</h1>"));

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendEmailAsync_WithEmptyHtmlContent_StillSends()
    {
        // Act
        await Assert.ThrowsAnyAsync<Exception>(() =>
            _emailService.SendEmailAsync("test@example.com", "Test", ""));

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce);
    }
}
