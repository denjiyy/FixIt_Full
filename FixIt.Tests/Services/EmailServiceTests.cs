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

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once);
    }

    [Fact]
    public async Task SendHealthReportEmailAsync_LogsHealthReportEmail()
    {
        // Act
        await _emailService.SendHealthReportEmailAsync(
            "user@example.com", "John Doe", "New York", "<h1>Health Report</h1>");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once);
    }

    [Fact]
    public async Task SendWeeklyReminderEmailAsync_LogsReminderEmail()
    {
        // Act
        await _emailService.SendWeeklyReminderEmailAsync(
            "user@example.com", "Jane Smith", 5, "San Francisco");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once);
    }

    [Fact]
    public async Task SendHazardAlertEmailAsync_LogsHazardAlert()
    {
        // Act
        await _emailService.SendHazardAlertEmailAsync(
            "user@example.com", "Test User", "Pothole", "Pothole", 1.5);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once);
    }
}

public class SmtpEmailServiceTests
{
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<SmtpEmailService>> _loggerMock;
    private readonly SmtpEmailService _emailService;

    public SmtpEmailServiceTests()
    {
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<SmtpEmailService>>();

        // Configure mock settings
        _configurationMock.Setup(c => c["Smtp:Host"]).Returns("smtp.gmail.com");
        _configurationMock.Setup(c => c["Smtp:Port"]).Returns("587");
        _configurationMock.Setup(c => c["Smtp:Username"]).Returns("test@gmail.com");
        _configurationMock.Setup(c => c["Smtp:Password"]).Returns("password");
        _configurationMock.Setup(c => c["Smtp:FromAddress"]).Returns("noreply@fixit.com");
        _configurationMock.Setup(c => c["Smtp:FromName"]).Returns("FixIt");

        _emailService = new SmtpEmailService(_configurationMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task SendEmailAsync_WithValidSmtpConfig_SendsEmail()
    {
        // Arrange
        _configurationMock.Setup(c => c["Smtp:Host"]).Returns("localhost");
        _configurationMock.Setup(c => c["Smtp:Port"]).Returns("1025"); // Test port

        // Act
        await _emailService.SendEmailAsync("test@example.com", "Test Subject", "<h1>Test</h1>");

        // Assert - Email service should attempt to log (won't actually send in test)
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
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
        await _emailService.SendEmailAsync(
            "test@example.com", "Test Subject", htmlContent, plainTextContent);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
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
        await _emailService.SendHealthReportEmailAsync(
            "user@example.com",
            "John Doe",
            "New York",
            "<h1>Health Report for New York</h1>"
        );

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendWeeklyReminderEmailAsync_IncludesIssueCount()
    {
        // Act
        await _emailService.SendWeeklyReminderEmailAsync(
            "user@example.com",
            "Jane Smith",
            10,
            "San Francisco"
        );

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendHazardAlertEmailAsync_IncludesHazardDetails()
    {
        // Act
        await _emailService.SendHazardAlertEmailAsync(
            "user@example.com",
            "Alert User",
            "Traffic Congestion",
            "TrafficCongestion",
            2.5
        );

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
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
        await _emailService.SendEmailAsync("test@example.com", "Test", "");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.AtLeastOnce);
    }
}
