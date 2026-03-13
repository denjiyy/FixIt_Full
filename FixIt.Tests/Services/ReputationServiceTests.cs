using Xunit;
using Moq;
using FixIt.Services.Gamification;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Gamification;
using FixIt.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FixIt.Tests.Services;

public class ReputationServiceTests
{
    private readonly Mock<IRepository<UserReputation>> _reputationRepoMock;
    private readonly Mock<IRepository<ReputationTransaction>> _transactionRepoMock;
    private readonly Mock<IRepository<LeaderboardEntry>> _leaderboardRepoMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ILogger<ReputationService>> _loggerMock;
    private readonly ReputationService _reputationService;

    public ReputationServiceTests()
    {
        _reputationRepoMock = new Mock<IRepository<UserReputation>>();
        _transactionRepoMock = new Mock<IRepository<ReputationTransaction>>();
        _leaderboardRepoMock = new Mock<IRepository<LeaderboardEntry>>();
        _loggerMock = new Mock<ILogger<ReputationService>>();

        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            It.IsAny<IUserStore<ApplicationUser>>(),
            It.IsAny<IOptions<IdentityOptions>>(),
            It.IsAny<IPasswordHasher<ApplicationUser>>(),
            It.IsAny<IEnumerable<IUserValidator<ApplicationUser>>>(),
            It.IsAny<IEnumerable<IPasswordValidator<ApplicationUser>>>(),
            It.IsAny<ILookupNormalizer>(),
            It.IsAny<IdentityErrorDescriber>(),
            It.IsAny<IServiceProvider>(),
            It.IsAny<ILogger<UserManager<ApplicationUser>>>());

        _reputationService = new ReputationService(
            _reputationRepoMock.Object,
            _transactionRepoMock.Object,
            _leaderboardRepoMock.Object,
            _userManagerMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task GetOrCreateUserReputationAsync_WhenNotExists_CreatesNewReputation()
    {
        // Arrange
        const string userId = "user1";
        UserReputation? savedReputation = null;

        _reputationRepoMock.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync((UserReputation?)null);

        _reputationRepoMock.Setup(r => r.InsertAsync(It.IsAny<UserReputation>()))
            .Callback<UserReputation>(rep => savedReputation = rep)
            .ReturnsAsync((UserReputation r) => r);

        // Act
        var result = await _reputationService.GetOrCreateUserReputationAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(0, result.TotalPoints);
        Assert.Equal(0, result.TrustLevel);
        _reputationRepoMock.Verify(r => r.InsertAsync(It.IsAny<UserReputation>()), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateUserReputationAsync_WhenExists_ReturnsExisting()
    {
        // Arrange
        const string userId = "user1";
        var existingReputation = new UserReputation 
        { 
            Id = userId, 
            UserId = userId, 
            TotalPoints = 100,
            TrustLevel = 2
        };

        _reputationRepoMock.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(existingReputation);

        // Act
        var result = await _reputationService.GetOrCreateUserReputationAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100, result.TotalPoints);
        Assert.Equal(2, result.TrustLevel);
        _reputationRepoMock.Verify(r => r.InsertAsync(It.IsAny<UserReputation>()), Times.Never);
    }

    [Fact]
    public async Task AddPointsAsync_IncrementsPointsCorrectly()
    {
        // Arrange
        const string userId = "user1";
        const int pointsToAdd = 50;
        const string reason = "issue_reported";
        
        var existingReputation = new UserReputation 
        { 
            Id = userId, 
            UserId = userId, 
            TotalPoints = 100,
            IssuesReported = 5
        };

        UserReputation? updatedReputation = null;

        _reputationRepoMock.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(existingReputation);

        _reputationRepoMock.Setup(r => r.InsertAsync(It.IsAny<UserReputation>()))
            .ReturnsAsync((UserReputation r) => r);

        _reputationRepoMock.Setup(r => r.ReplaceAsync(userId, It.IsAny<UserReputation>()))
            .Callback<string, UserReputation>((_, rep) => updatedReputation = rep)
            .Returns(Task.CompletedTask);

        _transactionRepoMock.Setup(r => r.InsertAsync(It.IsAny<ReputationTransaction>()))
            .ReturnsAsync((ReputationTransaction t) => t);

        // Act
        await _reputationService.AddPointsAsync(userId, pointsToAdd, reason);

        // Assert
        Assert.NotNull(updatedReputation);
        Assert.Equal(150, updatedReputation.TotalPoints); // 100 + 50
        Assert.Equal(6, updatedReputation.IssuesReported); // 5 + 1
        _transactionRepoMock.Verify(r => r.InsertAsync(It.IsAny<ReputationTransaction>()), Times.Once);
        _reputationRepoMock.Verify(r => r.ReplaceAsync(userId, It.IsAny<UserReputation>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task AddPointsAsync_WithMultipleReasons_UpdatesCorrectStat()
    {
        // Arrange
        const string userId = "user1";
        var reputation = new UserReputation 
        { 
            Id = userId, 
            UserId = userId, 
            TotalPoints = 0,
            CommentsPosted = 2
        };

        UserReputation? capturedReputation = null;

        _reputationRepoMock.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(reputation);

        _reputationRepoMock.Setup(r => r.InsertAsync(It.IsAny<UserReputation>()))
            .ReturnsAsync((UserReputation r) => r);

        _reputationRepoMock.Setup(r => r.ReplaceAsync(userId, It.IsAny<UserReputation>()))
            .Callback<string, UserReputation>((_, rep) => capturedReputation = rep)
            .Returns(Task.CompletedTask);

        _transactionRepoMock.Setup(r => r.InsertAsync(It.IsAny<ReputationTransaction>()))
            .ReturnsAsync((ReputationTransaction t) => t);

        // Act
        await _reputationService.AddPointsAsync(userId, 10, "comment_posted");

        // Assert
        Assert.NotNull(capturedReputation);
        Assert.Equal(10, capturedReputation.TotalPoints);
        Assert.Equal(3, capturedReputation.CommentsPosted); // 2 + 1
    }

    [Fact]
    public async Task GetUserReputationAsync_ReturnsUserReputation()
    {
        // Arrange
        const string userId = "user1";
        var reputation = new UserReputation 
        { 
            Id = userId, 
            UserId = userId, 
            TotalPoints = 250,
            TrustLevel = 5
        };

        _reputationRepoMock.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(reputation);

        // Act
        var result = await _reputationService.GetUserReputationAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(250, result.TotalPoints);
        Assert.Equal(5, result.TrustLevel);
    }

    [Fact]
    public async Task GetUserReputationAsync_WhenNotExists_ReturnsNull()
    {
        // Arrange
        const string userId = "nonexistent";

        _reputationRepoMock.Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync((UserReputation?)null);

        // Act
        var result = await _reputationService.GetUserReputationAsync(userId);

        // Assert
        Assert.Null(result);
    }
}
