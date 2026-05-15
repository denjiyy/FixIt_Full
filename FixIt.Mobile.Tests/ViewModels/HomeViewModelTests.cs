using FluentAssertions;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services.Contracts;
using FixIt.Mobile.Tests.TestSupport;
using FixIt.Mobile.ViewModels;
using Moq;
using Xunit;

namespace FixIt.Mobile.Tests.ViewModels;

public class HomeViewModelTests
{
    [Fact]
    public async Task OnLoadData_SetsStatCounts_FromIssueList()
    {
        var issues = new List<Issue>
        {
            new() { Status = AppConstants.StatusNew },
            new() { Status = AppConstants.StatusResolved },
            new() { Status = AppConstants.StatusInProgress },
            new() { Status = AppConstants.StatusCritical }
        };
        var vm = CreateViewModel(issues);

        await vm.LoadDashboardCommand.ExecuteAsync(null);

        vm.TotalIssues.Should().Be(4);
        vm.ResolvedIssues.Should().Be(1);
        vm.InProgressIssues.Should().Be(1);
        vm.CriticalIssues.Should().Be(1);
    }

    [Fact]
    public async Task OnLoadData_SetsRecentIssues_ToTopFive()
    {
        var issues = Enumerable.Range(1, 7).Select(i => new Issue { Id = i.ToString(), Title = $"Issue {i}" }).ToList();
        var vm = CreateViewModel(issues);

        await vm.LoadDashboardCommand.ExecuteAsync(null);

        vm.RecentIssues.Should().HaveCount(5);
        vm.RecentIssues.Select(i => i.Id).Should().Equal("1", "2", "3", "4", "5");
    }

    [Fact]
    public async Task OnLoadData_WhenApiReturnsEmpty_AllCountsAreZero()
    {
        var vm = CreateViewModel([]);

        await vm.LoadDashboardCommand.ExecuteAsync(null);

        vm.TotalIssues.Should().Be(0);
        vm.ResolvedIssues.Should().Be(0);
        vm.InProgressIssues.Should().Be(0);
        vm.CriticalIssues.Should().Be(0);
    }

    private static HomeViewModel CreateViewModel(List<Issue> issues)
    {
        var api = new Mock<IApiService>();
        api.Setup(x => x.GetIssuesAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(issues);

        var auth = new Mock<IAuthService>();
        auth.SetupGet(x => x.IsLoggedIn).Returns(false);

        return new HomeViewModel(api.Object, auth.Object, new NoopAnalyticsService(), new NoopPerformanceService());
    }
}
