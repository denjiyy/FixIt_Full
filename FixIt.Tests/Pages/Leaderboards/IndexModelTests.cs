using System.Linq.Expressions;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Gamification;
using FixIt.Models.Users;
using FixIt.Pages.Leaderboards;
using FixIt.Services.Gamification;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FixIt.Tests.Pages.Leaderboards;

public class IndexModelTests
{
    private readonly Mock<IReputationService> _reputationServiceMock = new();
    private readonly Mock<IRepository<LeaderboardEntry>> _leaderboardRepositoryMock = new();
    private readonly Mock<ILogger<IndexModel>> _loggerMock = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock =
        new(Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);

    private IndexModel CreateModel()
    {
        return new IndexModel(
            _reputationServiceMock.Object,
            _leaderboardRepositoryMock.Object,
            _userManagerMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task OnGetAsync_WhenLeaderboardsAreStale_RegeneratesAndReloadsFreshData()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var storageEntries = new List<LeaderboardEntry>
        {
            new() { UserId = "w", UserDisplayName = "Weekly", Rank = 1, Points = 10, Period = LeaderboardPeriod.Weekly, CreatedAt = now.AddHours(-2) },
            new() { UserId = "m", UserDisplayName = "Monthly", Rank = 1, Points = 20, Period = LeaderboardPeriod.Monthly, CreatedAt = now.AddHours(-2) },
            new() { UserId = "a", UserDisplayName = "All Time", Rank = 1, Points = 30, Period = LeaderboardPeriod.AllTime, CreatedAt = now.AddHours(-2) }
        };

        _leaderboardRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<LeaderboardEntry, bool>>>()))
            .ReturnsAsync((Expression<Func<LeaderboardEntry, bool>> predicate) =>
                storageEntries.Where(predicate.Compile()).ToList());

        _reputationServiceMock.SetupSequence(s => s.GetLeaderboardAsync(LeaderboardPeriod.Weekly, 50))
            .ReturnsAsync(new List<LeaderboardEntry> { new() { UserId = "w", UserDisplayName = "Weekly", Rank = 1, Points = 10, Period = LeaderboardPeriod.Weekly } })
            .ReturnsAsync(new List<LeaderboardEntry> { new() { UserId = "w2", UserDisplayName = "Weekly Fresh", Rank = 1, Points = 100, Period = LeaderboardPeriod.Weekly } });

        _reputationServiceMock.SetupSequence(s => s.GetLeaderboardAsync(LeaderboardPeriod.Monthly, 50))
            .ReturnsAsync(new List<LeaderboardEntry> { new() { UserId = "m", UserDisplayName = "Monthly", Rank = 1, Points = 20, Period = LeaderboardPeriod.Monthly } })
            .ReturnsAsync(new List<LeaderboardEntry> { new() { UserId = "m2", UserDisplayName = "Monthly Fresh", Rank = 1, Points = 200, Period = LeaderboardPeriod.Monthly } });

        _reputationServiceMock.SetupSequence(s => s.GetLeaderboardAsync(LeaderboardPeriod.AllTime, 50))
            .ReturnsAsync(new List<LeaderboardEntry> { new() { UserId = "a", UserDisplayName = "All Time", Rank = 1, Points = 30, Period = LeaderboardPeriod.AllTime } })
            .ReturnsAsync(new List<LeaderboardEntry> { new() { UserId = "a2", UserDisplayName = "All Time Fresh", Rank = 1, Points = 300, Period = LeaderboardPeriod.AllTime } });

        _reputationServiceMock.Setup(s => s.RegenerateWeeklyLeaderboardAsync()).Returns(Task.CompletedTask);
        _reputationServiceMock.Setup(s => s.RegenerateMonthlyLeaderboardAsync()).Returns(Task.CompletedTask);
        _reputationServiceMock.Setup(s => s.RegenerateAllTimeLeaderboardAsync()).Returns(Task.CompletedTask);

        var model = CreateModel();

        // Act
        await model.OnGetAsync();

        // Assert
        _reputationServiceMock.Verify(s => s.RegenerateWeeklyLeaderboardAsync(), Times.Once);
        _reputationServiceMock.Verify(s => s.RegenerateMonthlyLeaderboardAsync(), Times.Once);
        _reputationServiceMock.Verify(s => s.RegenerateAllTimeLeaderboardAsync(), Times.Once);
        _reputationServiceMock.Verify(s => s.GetLeaderboardAsync(LeaderboardPeriod.Weekly, 50), Times.Exactly(2));
        _reputationServiceMock.Verify(s => s.GetLeaderboardAsync(LeaderboardPeriod.Monthly, 50), Times.Exactly(2));
        _reputationServiceMock.Verify(s => s.GetLeaderboardAsync(LeaderboardPeriod.AllTime, 50), Times.Exactly(2));
        Assert.Equal("w2", model.WeeklyLeaderboard.Single().UserId);
        Assert.Equal("m2", model.MonthlyLeaderboard.Single().UserId);
        Assert.Equal("a2", model.AllTimeLeaderboard.Single().UserId);
    }

    [Fact]
    public async Task OnGetAsync_WhenLatestEntriesAreFresh_DoesNotRegenerate()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var storageEntries = new List<LeaderboardEntry>
        {
            // Intentionally put stale entry first to verify stale check uses latest timestamp, not first element.
            new() { UserId = "w-old", UserDisplayName = "Weekly old", Rank = 2, Points = 5, Period = LeaderboardPeriod.Weekly, CreatedAt = now.AddHours(-2) },
            new() { UserId = "w-new", UserDisplayName = "Weekly new", Rank = 1, Points = 10, Period = LeaderboardPeriod.Weekly, CreatedAt = now.AddMinutes(-5) },

            new() { UserId = "m-old", UserDisplayName = "Monthly old", Rank = 2, Points = 15, Period = LeaderboardPeriod.Monthly, CreatedAt = now.AddHours(-2) },
            new() { UserId = "m-new", UserDisplayName = "Monthly new", Rank = 1, Points = 20, Period = LeaderboardPeriod.Monthly, CreatedAt = now.AddMinutes(-5) },

            new() { UserId = "a-old", UserDisplayName = "All old", Rank = 2, Points = 25, Period = LeaderboardPeriod.AllTime, CreatedAt = now.AddHours(-2) },
            new() { UserId = "a-new", UserDisplayName = "All new", Rank = 1, Points = 30, Period = LeaderboardPeriod.AllTime, CreatedAt = now.AddMinutes(-5) }
        };

        _leaderboardRepositoryMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<LeaderboardEntry, bool>>>()))
            .ReturnsAsync((Expression<Func<LeaderboardEntry, bool>> predicate) =>
                storageEntries.Where(predicate.Compile()).ToList());

        _reputationServiceMock
            .Setup(s => s.GetLeaderboardAsync(It.IsAny<LeaderboardPeriod>(), 50))
            .ReturnsAsync((LeaderboardPeriod period, int _) =>
                storageEntries.Where(e => e.Period == period).OrderBy(e => e.Rank).ToList());

        var model = CreateModel();

        // Act
        await model.OnGetAsync();

        // Assert
        _reputationServiceMock.Verify(s => s.RegenerateWeeklyLeaderboardAsync(), Times.Never);
        _reputationServiceMock.Verify(s => s.RegenerateMonthlyLeaderboardAsync(), Times.Never);
        _reputationServiceMock.Verify(s => s.RegenerateAllTimeLeaderboardAsync(), Times.Never);
        _reputationServiceMock.Verify(s => s.GetLeaderboardAsync(LeaderboardPeriod.Weekly, 50), Times.Once);
        _reputationServiceMock.Verify(s => s.GetLeaderboardAsync(LeaderboardPeriod.Monthly, 50), Times.Once);
        _reputationServiceMock.Verify(s => s.GetLeaderboardAsync(LeaderboardPeriod.AllTime, 50), Times.Once);
    }
}
