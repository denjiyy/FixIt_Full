using Xunit;
using Moq;
using FixIt.Services.Safety;
using FixIt.Services.Gamification;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Safety;
using FixIt.Models.Enums;
using FixIt.Models.Users;
using MongoDB.Driver.GeoJsonObjectModel;

namespace FixIt.Tests.Services;

public class HazardServiceTests
{
    private readonly Mock<IRepository<Hazard>> _hazardRepoMock;
    private readonly Mock<IRepository<ApplicationUser>> _userRepoMock;
    private readonly Mock<IReputationService> _reputationServiceMock;
    private readonly HazardService _hazardService;

    public HazardServiceTests()
    {
        _hazardRepoMock = new Mock<IRepository<Hazard>>();
        _userRepoMock = new Mock<IRepository<ApplicationUser>>();
        _reputationServiceMock = new Mock<IReputationService>();

        _hazardService = new HazardService(
            _hazardRepoMock.Object,
            _userRepoMock.Object,
            _reputationServiceMock.Object
        );
    }

    private Hazard CreateTestHazard(string id = "hazard1", string cityId = "city1", 
        HazardType type = HazardType.Pothole, HazardSeverity severity = HazardSeverity.Medium,
        bool isDeleted = false, bool isResolved = false)
    {
        return new Hazard
        {
            Id = id,
            CityId = cityId,
            Type = type,
            Severity = severity,
            Title = "Test Hazard",
            Description = "Test hazard description",
            Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                new GeoJson2DGeographicCoordinates(-118.2437, 34.0522)),
            Address = "123 Main St",
            ReportedByUserId = "user1",
            IsAnonymous = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = isDeleted,
            IsResolved = isResolved
        };
    }

    [Fact]
    public async Task CreateHazardAsync_WithValidInputs_CreatesHazardSuccessfully()
    {
        // Arrange
        const string cityId = "city1";
        const HazardType type = HazardType.Pothole;
        const HazardSeverity severity = HazardSeverity.High;
        const string title = "Broken Pothole on Main St";
        const string description = "Large pothole is broken at intersection";
        const double latitude = 34.0522;
        const double longitude = -118.2437;
        const string userId = "user1";

        _hazardRepoMock.Setup(r => r.InsertAsync(It.IsAny<Hazard>()))
            .ReturnsAsync((Hazard h) => h);

        // Act
        var result = await _hazardService.CreateHazardAsync(
            cityId, type, severity, title, description, latitude, longitude, userId: userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(title, result.Title);
        Assert.Equal(cityId, result.CityId);
        Assert.Equal(type, result.Type);
        Assert.Equal(severity, result.Severity);
        Assert.Equal(userId, result.ReportedByUserId);
        _hazardRepoMock.Verify(r => r.InsertAsync(It.IsAny<Hazard>()), Times.Once);
    }

    [Fact]
    public async Task CreateHazardAsync_WithAnonymousReport_CreatesWithoutUserId()
    {
        // Arrange
        _hazardRepoMock.Setup(r => r.InsertAsync(It.IsAny<Hazard>()))
            .ReturnsAsync((Hazard h) => h);

        // Act
        var result = await _hazardService.CreateHazardAsync(
            "city1", HazardType.Accident, HazardSeverity.Medium,
            "Anonymous Report", "Hazard description", 34.0522, -118.2437, isAnonymous: true);

        // Assert
        Assert.Null(result.ReportedByUserId);
        Assert.True(result.IsAnonymous);
    }

    [Fact]
    public async Task GetHazardAsync_WithExistingHazard_ReturnsHazard()
    {
        // Arrange
        var hazard = CreateTestHazard();
        _hazardRepoMock.Setup(r => r.GetByIdAsync("hazard1"))
            .ReturnsAsync(hazard);

        // Act
        var result = await _hazardService.GetHazardAsync("hazard1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("hazard1", result.Id);
        Assert.Equal("Test Hazard", result.Title);
    }

    [Fact]
    public async Task GetHazardAsync_WithDeletedHazard_ReturnsNull()
    {
        // Arrange
        var hazard = CreateTestHazard(isDeleted: true);
        _hazardRepoMock.Setup(r => r.GetByIdAsync("hazard1"))
            .ReturnsAsync(hazard);

        // Act
        var result = await _hazardService.GetHazardAsync("hazard1");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCityHazardsAsync_ReturnsActiveHazards()
    {
        // Arrange
        var hazards = new List<Hazard>
        {
            CreateTestHazard("h1", "city1"),
            CreateTestHazard("h2", "city1"),
            CreateTestHazard("h3", "city2") // Different city
        };

        _hazardRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Hazard, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Hazard, bool>> expr) =>
                hazards.Where(expr.Compile()).ToList());

        // Act
        var result = await _hazardService.GetCityHazardsAsync("city1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetActiveSafetyHazardsAsync_ReturnsOnlyHighCriticalHazards()
    {
        // Arrange
        var hazards = new List<Hazard>
        {
            CreateTestHazard("h1", "city1", severity: HazardSeverity.Critical),
            CreateTestHazard("h2", "city1", severity: HazardSeverity.High),
            CreateTestHazard("h3", "city1", severity: HazardSeverity.Medium) // Should not be included
        };

        _hazardRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Hazard, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Hazard, bool>> expr) =>
                hazards.Where(expr.Compile()).ToList());

        // Act
        var result = await _hazardService.GetActiveSafetyHazardsAsync("city1");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, h => Assert.True(
            h.Severity == HazardSeverity.Critical || h.Severity == HazardSeverity.High));
    }

    [Fact]
    public async Task ConfirmHazardAsync_IncrementsConfirmationCount()
    {
        // Arrange
        var hazard = CreateTestHazard();
        hazard.ConfirmedByUserIds = new HashSet<string>();
        hazard.Confirmations = 0;

        _hazardRepoMock.Setup(r => r.GetByIdAsync("hazard1"))
            .ReturnsAsync(hazard);
        _hazardRepoMock.Setup(r => r.ReplaceAsync("hazard1", It.IsAny<Hazard>()))
            .Returns(Task.CompletedTask);
        _reputationServiceMock.Setup(r => r.AddPointsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _hazardService.ConfirmHazardAsync("hazard1", "user2");

        // Assert
        Assert.True(result);
        Assert.Contains("user2", hazard.ConfirmedByUserIds);
        _hazardRepoMock.Verify(r => r.ReplaceAsync("hazard1", It.IsAny<Hazard>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmHazardAsync_AwardsPointsOnFirstConfirmation()
    {
        // Arrange
        var hazard = CreateTestHazard();
        hazard.ConfirmedByUserIds = new HashSet<string>();
        hazard.Confirmations = 0;
        hazard.ReportedByUserId = "reporter1";

        _hazardRepoMock.Setup(r => r.GetByIdAsync("hazard1"))
            .ReturnsAsync(hazard);
        _hazardRepoMock.Setup(r => r.ReplaceAsync("hazard1", It.IsAny<Hazard>()))
            .Returns(Task.CompletedTask);
        _reputationServiceMock.Setup(r => r.AddPointsAsync("reporter1", 10, "hazard_confirmed", "hazard1", null))
            .Returns(Task.CompletedTask);

        // Act
        await _hazardService.ConfirmHazardAsync("hazard1", "user2");

        // Assert
        _reputationServiceMock.Verify(
            r => r.AddPointsAsync("reporter1", 10, "hazard_confirmed", "hazard1", null),
            Times.Once);
    }

    [Fact]
    public async Task ResolveHazardAsync_MarksHazardAsResolved()
    {
        // Arrange
        var hazard = CreateTestHazard();
        var user = new ApplicationUser
        {
            Id = MongoDB.Bson.ObjectId.GenerateNewId(),
            DisplayName = "Test User",
            Email = "test@example.com",
            Role = UserRole.Admin
        };
        
        _hazardRepoMock.Setup(r => r.GetByIdAsync("hazard1"))
            .ReturnsAsync(hazard);
        _hazardRepoMock.Setup(r => r.ReplaceAsync("hazard1", It.IsAny<Hazard>()))
            .Returns(Task.CompletedTask);
        _userRepoMock.Setup(r => r.GetByIdAsync("user1"))
            .ReturnsAsync(user);
        _reputationServiceMock.Setup(r => r.AddPointsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _hazardService.ResolveHazardAsync("hazard1", "user1", "Fixed by city");

        // Assert
        Assert.True(result);
        Assert.True(hazard.IsResolved);
        Assert.Equal("user1", hazard.ResolvedByUserId);
        Assert.Equal("Fixed by city", hazard.ResolutionNotes);
    }

    [Fact]
    public async Task GetHazardBreakdownAsync_ReturnsTypeDistribution()
    {
        // Arrange
        var hazards = new List<Hazard>
        {
            CreateTestHazard("h1", "city1", HazardType.Pothole),
            CreateTestHazard("h2", "city1", HazardType.Pothole),
            CreateTestHazard("h3", "city1", HazardType.Accident)
        };

        _hazardRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Hazard, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Hazard, bool>> expr) =>
                hazards.Where(expr.Compile()).ToList());

        // Act
        var result = await _hazardService.GetHazardBreakdownAsync("city1");

        // Assert
        Assert.Equal(2, result[HazardType.Pothole]);
        Assert.Equal(1, result[HazardType.Accident]);
    }

    [Fact]
    public async Task SearchHazardsAsync_WithTypeFilter_ReturnsMatchingHazards()
    {
        // Arrange
        var hazards = new List<Hazard>
        {
            CreateTestHazard("h1", "city1", HazardType.Pothole),
            CreateTestHazard("h2", "city1", HazardType.Accident)
        };

        _hazardRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Hazard, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Hazard, bool>> expr) =>
                hazards.Where(expr.Compile()).ToList());

        // Act
        var result = await _hazardService.SearchHazardsAsync("city1", HazardType.Pothole);

        // Assert
        Assert.Single(result);
        Assert.Equal(HazardType.Pothole, result[0].Type);
    }

    [Fact]
    public async Task SoftDeleteHazardAsync_MarkAsDeleted()
    {
        // Arrange
        var hazard = CreateTestHazard();
        _hazardRepoMock.Setup(r => r.GetByIdAsync("hazard1"))
            .ReturnsAsync(hazard);
        _hazardRepoMock.Setup(r => r.ReplaceAsync("hazard1", It.IsAny<Hazard>()))
            .Returns(Task.CompletedTask);

        // Act
        await _hazardService.SoftDeleteHazardAsync("hazard1", "admin1");

        // Assert
        Assert.True(hazard.IsDeleted);
        Assert.NotNull(hazard.DeletedAt);
        Assert.Equal("admin1", hazard.DeletedByUserId);
    }

    [Fact]
    public async Task RestoreHazardAsync_ClearsDeletedFlags()
    {
        // Arrange
        var hazard = CreateTestHazard(isDeleted: true);
        hazard.DeletedAt = DateTime.UtcNow;
        hazard.DeletedByUserId = "admin1";

        _hazardRepoMock.Setup(r => r.GetByIdAsync("hazard1"))
            .ReturnsAsync(hazard);
        _hazardRepoMock.Setup(r => r.ReplaceAsync("hazard1", It.IsAny<Hazard>()))
            .Returns(Task.CompletedTask);

        // Act
        await _hazardService.RestoreHazardAsync("hazard1");

        // Assert
        Assert.False(hazard.IsDeleted);
        Assert.Null(hazard.DeletedAt);
        Assert.Null(hazard.DeletedByUserId);
    }

    [Fact]
    public async Task UpdateHazardAsync_UpdatesMultipleFields()
    {
        // Arrange
        var hazard = CreateTestHazard();
        var originalVersion = hazard.Version;

        _hazardRepoMock.Setup(r => r.GetByIdAsync("hazard1"))
            .ReturnsAsync(hazard);
        _hazardRepoMock.Setup(r => r.ReplaceAsync("hazard1", It.IsAny<Hazard>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _hazardService.UpdateHazardAsync("hazard1",
            type: HazardType.Construction,
            title: "Updated Title");

        // Assert
        Assert.Equal(HazardType.Construction, result.Type);
        Assert.Equal("Updated Title", result.Title);
        Assert.Equal(originalVersion + 1, result.Version);
    }

    [Fact]
    public async Task GetNearbyHazardsAsync_ReturnsHazardsWithinRadius()
    {
        // Arrange
        var hazards = new List<Hazard>
        {
            CreateTestHazard("h1", "city1"), // At 34.0522, -118.2437
            CreateTestHazard("h2", "city1")
        };
        
        // Set one hazard at same location, one far away
        hazards[0].Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
            new GeoJson2DGeographicCoordinates(-118.2437, 34.0522));
        hazards[1].Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
            new GeoJson2DGeographicCoordinates(-118.2437, 40.0522)); // ~668km away

        _hazardRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Hazard, bool>>>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<Hazard, bool>> expr) =>
                hazards.Where(expr.Compile()).ToList());

        // Act
        var result = await _hazardService.GetNearbyHazardsAsync("city1", 34.0522, -118.2437, radiusKm: 100);

        // Assert - should only return the one at same location
        Assert.Single(result);
    }
}
