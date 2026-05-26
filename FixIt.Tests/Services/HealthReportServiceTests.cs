using Xunit;
using Moq;
using FixIt.Services.Analytics;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.Locations;
using FixIt.Models.Enums;
using FixIt.Models.Common;

namespace FixIt.Tests.Services;

public class HealthReportServiceTests
{
    private readonly Mock<IRepository<Issue>> _issueRepoMock;
    private readonly Mock<IRepository<City>> _cityRepoMock;
    private readonly HealthReportService _healthReportService;

    public HealthReportServiceTests()
    {
        _issueRepoMock = new Mock<IRepository<Issue>>();
        _cityRepoMock = new Mock<IRepository<City>>();

        _healthReportService = new HealthReportService(
            _issueRepoMock.Object,
            _cityRepoMock.Object
        );
    }

    private Issue CreateTestIssue(string id = "issue1", string cityId = "city1",
        IssueStatus status = IssueStatus.New, IssuePriority priority = IssuePriority.Medium,
        DateTime? createdAt = null, DateTime? updatedAt = null)
    {
        var now = createdAt ?? DateTime.UtcNow;
        return new Issue
        {
            Id = id,
            CityId = cityId,
            Title = "Test Issue",
            Status = status,
            Priority = priority,
            CreatedAt = now,
            UpdatedAt = updatedAt ?? now,
            Reporter = new UserSummary { Id = "user1", DisplayName = "Test User" },
            Upvotes = 0,
            CommentCount = 0
        };
    }

    [Fact]
    public async Task GetCityHealthReportAsync_WithOpenIssues_CalculatesHealthScore()
    {
        // Arrange
        var city = new City { Id = "city1", Name = "Test City" };
        var issues = new List<Issue>
        {
            CreateTestIssue("i1", "city1", IssueStatus.New),
            CreateTestIssue("i2", "city1", IssueStatus.New)
        };

        _cityRepoMock.Setup(r => r.GetByIdAsync("city1"))
            .ReturnsAsync(city);
        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).ToList());
        _issueRepoMock.Setup(r => r.CountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).LongCount());

        // Act
        var result = await _healthReportService.GetCityHealthReportAsync("city1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("city1", result.CityId);
        Assert.Equal("Test City", result.CityName);
        Assert.Equal(2, result.TotalIssues);
        Assert.Equal(0, result.ResolvedIssues);
        Assert.Equal(0, result.HealthScore); // 100 - (open/total)*100 == 100 - (2/2)*100
    }

    [Fact]
    public async Task GetCityHealthReportAsync_WithNoOpenIssues_MaximumHealthScore()
    {
        // Arrange
        var city = new City { Id = "city1", Name = "Test City" };
        var issues = new List<Issue>
        {
            CreateTestIssue("i1", "city1", IssueStatus.Fixed),
            CreateTestIssue("i2", "city1", IssueStatus.Fixed)
        };

        _cityRepoMock.Setup(r => r.GetByIdAsync("city1"))
            .ReturnsAsync(city);
        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).ToList());
        _issueRepoMock.Setup(r => r.CountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).LongCount());

        // Act
        var result = await _healthReportService.GetCityHealthReportAsync("city1");

        // Assert
        Assert.Equal(2, result.ResolvedIssues);
        Assert.Equal(100, result.HealthScore); // Perfect health
    }

    [Fact]
    public async Task GetCityHealthReportAsync_CalculatesResolutionRate()
    {
        // Arrange
        var city = new City { Id = "city1", Name = "Test City" };
        var issues = new List<Issue>
        {
            CreateTestIssue("i1", "city1", IssueStatus.Fixed),
            CreateTestIssue("i2", "city1", IssueStatus.Fixed),
            CreateTestIssue("i3", "city1", IssueStatus.New),
            CreateTestIssue("i4", "city1", IssueStatus.New)
        };

        _cityRepoMock.Setup(r => r.GetByIdAsync("city1"))
            .ReturnsAsync(city);
        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).ToList());
        _issueRepoMock.Setup(r => r.CountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).LongCount());

        // Act
        var result = await _healthReportService.GetCityHealthReportAsync("city1");

        // Assert
        Assert.Equal(50, result.ResolutionRate); // 2 resolved / 4 total = 50%
    }

    [Fact]
    public async Task GetCityHealthReportAsync_CountsPriorityDistribution()
    {
        // Arrange
        var city = new City { Id = "city1", Name = "Test City" };
        var issues = new List<Issue>
        {
            CreateTestIssue("i1", "city1", priority: IssuePriority.Critical),
            CreateTestIssue("i2", "city1", priority: IssuePriority.High),
            CreateTestIssue("i3", "city1", priority: IssuePriority.Medium)
        };

        _cityRepoMock.Setup(r => r.GetByIdAsync("city1"))
            .ReturnsAsync(city);
        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).ToList());
        _issueRepoMock.Setup(r => r.CountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).LongCount());

        // Act
        var result = await _healthReportService.GetCityHealthReportAsync("city1");

        // Assert
        Assert.Equal(1, result.CriticalIssues);
        Assert.Equal(1, result.HighIssues);
        Assert.Equal(1, result.MediumIssues);
        Assert.Equal(0, result.LowIssues);
    }

    [Fact]
    public async Task GetCityHealthReportAsync_CalculatesAverageResolutionTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var city = new City { Id = "city1", Name = "Test City" };
        var issues = new List<Issue>
        {
            CreateTestIssue("i1", "city1", IssueStatus.Fixed,
                createdAt: now.AddDays(-2), updatedAt: now), // 48 hours
            CreateTestIssue("i2", "city1", IssueStatus.Fixed,
                createdAt: now.AddDays(-1), updatedAt: now)  // 24 hours
        };

        _cityRepoMock.Setup(r => r.GetByIdAsync("city1"))
            .ReturnsAsync(city);
        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).ToList());

        // Act
        var result = await _healthReportService.GetCityHealthReportAsync("city1");

        // Assert
        Assert.Equal(36, result.AverageResolutionTimeHours); // (48 + 24) / 2 = 36
    }

    [Fact]
    public async Task GetCityHealthReportAsync_CountsEngagementMetrics()
    {
        // Arrange
        var city = new City { Id = "city1", Name = "Test City" };
        var issues = new List<Issue>
        {
            new Issue { Id = "i1", CityId = "city1", CommentCount = 5, Upvotes = 10, Reporter = new UserSummary { Id = "u1" } },
            new Issue { Id = "i2", CityId = "city1", CommentCount = 3, Upvotes = 7, Reporter = new UserSummary { Id = "u1" } }
        };

        _cityRepoMock.Setup(r => r.GetByIdAsync("city1"))
            .ReturnsAsync(city);
        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).ToList());
        _issueRepoMock.Setup(r => r.CountAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).LongCount());

        // Act
        var result = await _healthReportService.GetCityHealthReportAsync("city1");

        // Assert
        Assert.Equal(8, result.TotalComments);
        Assert.Equal(17, result.TotalUpvotes);
        Assert.Equal(8, result.AverageUpvotesPerIssue);
    }

    [Fact]
    public async Task GetGlobalHealthReportAsync_AggregatesAllCities()
    {
        // Arrange
        var issues = new List<Issue>
        {
            CreateTestIssue("i1", "city1", IssueStatus.New),
            CreateTestIssue("i2", "city1", IssueStatus.Fixed),
            CreateTestIssue("i3", "city2", IssueStatus.New)
        };

        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).ToList());
        _cityRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<City, bool>>>()))
            .ReturnsAsync(new List<City>());

        // Act
        var result = await _healthReportService.GetGlobalHealthReportAsync();

        // Assert
        Assert.Equal("global", result.CityId);
        Assert.Equal("Global", result.CityName);
        Assert.Equal(3, result.TotalIssues);
        Assert.Equal(1, result.ResolvedIssues);
    }

    [Fact]
    public async Task GetCityHealthReportAsync_WithNoIssues_ReturnsZeroMetrics()
    {
        // Arrange
        var city = new City { Id = "city1", Name = "Test City" };
        var issues = new List<Issue>();

        _cityRepoMock.Setup(r => r.GetByIdAsync("city1"))
            .ReturnsAsync(city);
        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync(issues);

        // Act
        var result = await _healthReportService.GetCityHealthReportAsync("city1");

        // Assert
        Assert.Equal(0, result.TotalIssues);
        Assert.Equal(0, result.ResolvedIssues);
        Assert.Equal(100, result.HealthScore); // Perfect health with no issues
        Assert.Equal(0, result.ResolutionRate);
    }

    [Fact]
    public async Task GetCityHealthReportAsync_IncludesStatusBreakdown()
    {
        // Arrange
        var city = new City { Id = "city1", Name = "Test City" };
        var issues = new List<Issue>
        {
            CreateTestIssue("i1", "city1", IssueStatus.New),
            CreateTestIssue("i2", "city1", IssueStatus.Fixed),
            CreateTestIssue("i3", "city1", IssueStatus.InProgress)
        };

        _cityRepoMock.Setup(r => r.GetByIdAsync("city1"))
            .ReturnsAsync(city);
        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).ToList());

        // Act
        var result = await _healthReportService.GetCityHealthReportAsync("city1");

        // Assert
        Assert.NotNull(result.IssuesByStatus);
        Assert.Contains("New", result.IssuesByStatus.Keys);
        Assert.Contains("Fixed", result.IssuesByStatus.Keys);
    }
}
