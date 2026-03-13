using Xunit;
using Moq;
using FixIt.Services.Analytics;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.Locations;
using FixIt.Models.Enums;
using FixIt.Models.Common;
using FixIt.Services.Analytics.Models;
using MongoDB.Driver.GeoJsonObjectModel;

namespace FixIt.Tests.Services;

public class HeatmapServiceTests
{
    private readonly Mock<IRepository<Issue>> _issueRepoMock;
    private readonly Mock<IRepository<City>> _cityRepoMock;
    private readonly Mock<IRepository<Tag>> _tagRepoMock;
    private readonly HeatmapService _heatmapService;

    public HeatmapServiceTests()
    {
        _issueRepoMock = new Mock<IRepository<Issue>>();
        _cityRepoMock = new Mock<IRepository<City>>();
        _tagRepoMock = new Mock<IRepository<Tag>>();

        _heatmapService = new HeatmapService(
            _issueRepoMock.Object,
            _cityRepoMock.Object,
            _tagRepoMock.Object
        );
    }

    private Issue CreateTestIssue(string id = "issue1", string cityId = "city1",
        IssueStatus status = IssueStatus.New, IssuePriority priority = IssuePriority.Medium,
        DateTime? createdAt = null, double lat = 34.0522, double lon = -118.2437)
    {
        return new Issue
        {
            Id = id,
            CityId = cityId,
            Title = "Test Issue",
            Description = "Test Description",
            Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                new GeoJson2DGeographicCoordinates(lon, lat)),
            Status = status,
            Priority = priority,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Reporter = new UserSummary { Id = "user1", DisplayName = "Test User" },
            TagIds = new HashSet<string>()
        };
    }

    [Fact]
    public async Task GetCityHeatmapAsync_WithIssuesInDateRange_ReturnsHeatmapData()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var issues = new List<Issue>
        {
            CreateTestIssue("i1", "city1", IssueStatus.New, createdAt: now.AddDays(-5)),
            CreateTestIssue("i2", "city1", IssueStatus.Fixed, createdAt: now.AddDays(-3)),
            CreateTestIssue("i3", "city2", IssueStatus.New, createdAt: now.AddDays(-1))
        };

        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).ToList());
        
        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(new List<Tag>());

        // Act
        var result = await _heatmapService.GetCityHeatmapAsync("city1", now.AddDays(-30), now);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("city1", result.CityId);
        Assert.Equal(2, result.TotalIssues);
        Assert.Equal(1, result.ResolvedIssues);
    }

    [Fact]
    public async Task GetIssueHotspots_GroupsIssuesByLocation()
    {
        // Arrange
        var issues = new List<Issue>
        {
            CreateTestIssue("i1", "city1", lat: 34.0522, lon: -118.2437),
            CreateTestIssue("i2", "city1", lat: 34.0522, lon: -118.2437), // Same location
            CreateTestIssue("i3", "city1", lat: 35.0522, lon: -117.2437)  // Different location
        };

        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync(issues);

        // Act
        var result = await _heatmapService.GetIssueHotspots("city1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count); // 2 unique locations
        Assert.Equal(2, result[0].Intensity); // First hotspot has 2 issues
    }

    [Fact]
    public async Task GetIssueHotspots_ExcludesResolvedIssues()
    {
        // Arrange
        var issues = new List<Issue>
        {
            CreateTestIssue("i1", "city1", IssueStatus.New),
            CreateTestIssue("i2", "city1", IssueStatus.Fixed), // Should be excluded
            CreateTestIssue("i3", "city1", IssueStatus.InProgress)
        };

        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync(issues);

        // Act
        var result = await _heatmapService.GetIssueHotspots("city1");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, h => Assert.NotEqual(IssueStatus.Fixed, h.Status));
    }

    [Fact]
    public async Task GetIssuesByTag_CountsIssuesByTag()
    {
        // Arrange
        var issues = new List<Issue>
        {
            new Issue { Id = "i1", CityId = "city1", TagIds = new HashSet<string> { "tag1", "tag2" } },
            new Issue { Id = "i2", CityId = "city1", TagIds = new HashSet<string> { "tag1" } },
            new Issue { Id = "i3", CityId = "city1", TagIds = new HashSet<string>() }
        };

        var tags = new List<Tag>
        {
            new Tag { Id = "tag1", Name = "pothole" },
            new Tag { Id = "tag2", Name = "safety" }
        };

        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync(issues);
        
        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(tags);

        // Act
        var result = await _heatmapService.GetIssuesByTag("city1");

        // Assert
        Assert.Equal(2, result["pothole"]); // tag1 appears in 2 issues
        Assert.Equal(1, result["safety"]);  // tag2 appears in 1 issue
    }

    [Fact]
    public async Task GetIssuesByTag_WithUntaggedIssues_ReturnsUntaggedCount()
    {
        // Arrange
        var issues = new List<Issue>
        {
            new Issue { Id = "i1", CityId = "city1", TagIds = new HashSet<string>() },
            new Issue { Id = "i2", CityId = "city1", TagIds = new HashSet<string>() }
        };

        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync(issues);
        
        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(new List<Tag>());

        // Act
        var result = await _heatmapService.GetIssuesByTag("city1");

        // Assert
        Assert.Contains("Untagged", result.Keys);
        Assert.Equal(2, result["Untagged"]);
    }

    [Fact]
    public async Task GetIssueHotspots_LimitsResults()
    {
        // Arrange
        var issues = Enumerable.Range(1, 150)
            .Select(i => CreateTestIssue($"i{i}", "city1", lat: 34.0522 + (i * 0.001), lon: -118.2437))
            .ToList();

        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync(issues);

        // Act
        var result = await _heatmapService.GetIssueHotspots("city1", limit: 50);

        // Assert
        Assert.True(result.Count <= 50);
    }

    [Fact]
    public async Task GetCityHeatmapAsync_CalculatesCorrectMetrics()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var issues = new List<Issue>
        {
            CreateTestIssue("i1", "city1", IssueStatus.New, createdAt: now),
            CreateTestIssue("i2", "city1", IssueStatus.New, createdAt: now),
            CreateTestIssue("i3", "city1", IssueStatus.Fixed, createdAt: now.AddDays(-50))
        };

        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync(issues);
        
        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(new List<Tag>());

        // Act
        var result = await _heatmapService.GetCityHeatmapAsync("city1");

        // Assert
        Assert.Equal(3, result.TotalIssues);
        Assert.Equal(1, result.ResolvedIssues);
        Assert.Equal(2, result.OpenIssues);
    }
}
