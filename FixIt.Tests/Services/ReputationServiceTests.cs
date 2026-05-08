using Xunit;
using Moq;
using FixIt.Services.Gamification;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Gamification;
using FixIt.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;

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
        var optionsMock = Options.Create(new IdentityOptions());
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var userValidators = new List<IUserValidator<ApplicationUser>>();
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>>();
        var lookupNormalizer = new UpperInvariantLookupNormalizer();
        var identityErrors = new IdentityErrorDescriber();
        var serviceProvider = new Mock<IServiceProvider>().Object;
        var userManagerLogger = new Mock<ILogger<UserManager<ApplicationUser>>>().Object;

        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object,
            optionsMock,
            passwordHasher,
            userValidators,
            passwordValidators,
            lookupNormalizer,
            identityErrors,
            serviceProvider,
            userManagerLogger);

        // Mock UserManager methods
        _userManagerMock.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new ApplicationUser { Id = MongoDB.Bson.ObjectId.GenerateNewId(), Email = $"user_{id}@example.com", EmailConfirmed = true });

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
        Assert.Equal(215, updatedReputation.TotalPoints); // 100 + 50 + achievement rewards (65)
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
        Assert.Equal(15, capturedReputation.TotalPoints); // 10 + verified-email achievement reward (5)
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

    [Fact]
    public async Task GetLeaderboardAsync_OrdersByRankAndHonorsTakeLimit()
    {
        // Arrange
        var allEntries = new List<LeaderboardEntry>
        {
            new() { UserId = "u3", UserDisplayName = "User 3", Rank = 3, Points = 30, Period = LeaderboardPeriod.Weekly },
            new() { UserId = "u1", UserDisplayName = "User 1", Rank = 1, Points = 50, Period = LeaderboardPeriod.Weekly },
            new() { UserId = "u2", UserDisplayName = "User 2", Rank = 2, Points = 40, Period = LeaderboardPeriod.Weekly },
            new() { UserId = "m1", UserDisplayName = "Monthly", Rank = 1, Points = 100, Period = LeaderboardPeriod.Monthly }
        };

        _leaderboardRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<LeaderboardEntry, bool>>>()))
            .ReturnsAsync((Expression<Func<LeaderboardEntry, bool>> predicate) =>
                allEntries.Where(predicate.Compile()).ToList());

        // Act
        var result = await _reputationService.GetLeaderboardAsync(LeaderboardPeriod.Weekly, 2);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("u1", result[0].UserId);
        Assert.Equal("u2", result[1].UserId);
    }

    [Fact]
    public async Task RegenerateMonthlyLeaderboardAsync_SkipsMissingUsersAndKeepsRanksContiguous()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var transactions = new List<ReputationTransaction>
        {
            new() { UserId = "missing-user", Points = 100, CreatedAt = now.AddDays(-1) },
            new() { UserId = "user-a", Points = 80, CreatedAt = now.AddDays(-1) },
            new() { UserId = "user-b", Points = 60, CreatedAt = now.AddDays(-1) }
        };

        var userReputations = new Dictionary<string, UserReputation>
        {
            ["user-a"] = new() { Id = "user-a", UserId = "user-a", TrustLevel = 2, TotalPoints = 250 },
            ["user-b"] = new() { Id = "user-b", UserId = "user-b", TrustLevel = 1, TotalPoints = 80 }
        };

        var insertedEntries = new List<LeaderboardEntry>();

        _transactionRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<ReputationTransaction, bool>>>()))
            .ReturnsAsync((Expression<Func<ReputationTransaction, bool>> predicate) =>
                transactions.Where(predicate.Compile()).ToList());

        _leaderboardRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<LeaderboardEntry, bool>>>()))
            .ReturnsAsync(new List<LeaderboardEntry>());

        _leaderboardRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<LeaderboardEntry>()))
            .Callback<LeaderboardEntry>(entry => insertedEntries.Add(entry))
            .ReturnsAsync((LeaderboardEntry e) => e);

        _reputationRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => userReputations.TryGetValue(id, out var rep) ? rep : null);

        _userManagerMock.Setup(m => m.FindByIdAsync("missing-user"))
            .ReturnsAsync((ApplicationUser?)null);
        _userManagerMock.Setup(m => m.FindByIdAsync("user-a"))
            .ReturnsAsync(new ApplicationUser
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                DisplayName = "User A",
                EmailConfirmed = true
            });
        _userManagerMock.Setup(m => m.FindByIdAsync("user-b"))
            .ReturnsAsync(new ApplicationUser
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                DisplayName = "User B",
                EmailConfirmed = true
            });

        // Act
        await _reputationService.RegenerateMonthlyLeaderboardAsync();

        // Assert
        Assert.Equal(2, insertedEntries.Count);
        Assert.Equal("user-a", insertedEntries[0].UserId);
        Assert.Equal("user-b", insertedEntries[1].UserId);
        Assert.Equal(1, insertedEntries[0].Rank);
        Assert.Equal(2, insertedEntries[1].Rank);
        Assert.DoesNotContain(insertedEntries, e => e.UserId == "missing-user");
    }

    [Fact]
    public async Task RegenerateWeeklyLeaderboardAsync_WhenPointsTie_OrdersByUserIdDeterministically()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var transactions = new List<ReputationTransaction>
        {
            new() { UserId = "user-b", Points = 10, CreatedAt = now.AddDays(-1) },
            new() { UserId = "user-a", Points = 10, CreatedAt = now.AddDays(-1) }
        };

        var insertedEntries = new List<LeaderboardEntry>();

        _transactionRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<ReputationTransaction, bool>>>()))
            .ReturnsAsync((Expression<Func<ReputationTransaction, bool>> predicate) =>
                transactions.Where(predicate.Compile()).ToList());

        _leaderboardRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<LeaderboardEntry, bool>>>()))
            .ReturnsAsync(new List<LeaderboardEntry>());

        _leaderboardRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<LeaderboardEntry>()))
            .Callback<LeaderboardEntry>(entry => insertedEntries.Add(entry))
            .ReturnsAsync((LeaderboardEntry e) => e);

        _reputationRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((UserReputation?)null);

        _userManagerMock.Setup(m => m.FindByIdAsync("user-a"))
            .ReturnsAsync(new ApplicationUser
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                DisplayName = "User A",
                EmailConfirmed = true
            });
        _userManagerMock.Setup(m => m.FindByIdAsync("user-b"))
            .ReturnsAsync(new ApplicationUser
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId(),
                DisplayName = "User B",
                EmailConfirmed = true
            });

        // Act
        await _reputationService.RegenerateWeeklyLeaderboardAsync();

        // Assert
        Assert.Equal(2, insertedEntries.Count);
        Assert.Equal("user-a", insertedEntries[0].UserId);
        Assert.Equal("user-b", insertedEntries[1].UserId);
        Assert.Equal(1, insertedEntries[0].Rank);
        Assert.Equal(2, insertedEntries[1].Rank);
    }
}
