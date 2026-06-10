using FluentAssertions;
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
    public async Task Refresh_PopulatesFeed_FromApi()
    {
        var issues = Enumerable.Range(1, 3)
            .Select(i => new Issue { Id = i.ToString(), Title = $"Issue {i}" })
            .ToList();
        var vm = CreateViewModel(issues);

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.FeedIssues.Should().HaveCount(3);
        vm.FeedIssues.Select(i => i.Id).Should().Equal("1", "2", "3");
        vm.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_WhenApiReturnsEmpty_SetsIsEmpty()
    {
        var vm = CreateViewModel([]);

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.FeedIssues.Should().BeEmpty();
        vm.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task Refresh_ClearsLoadingFlagWhenDone()
    {
        var vm = CreateViewModel([new Issue { Id = "1", Title = "Issue 1" }]);

        await vm.RefreshCommand.ExecuteAsync(null);

        vm.IsLoading.Should().BeFalse();
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
