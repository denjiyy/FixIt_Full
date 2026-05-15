using FluentAssertions;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services.Contracts;
using FixIt.Mobile.Tests.TestSupport;
using FixIt.Mobile.ViewModels;
using Moq;
using Xunit;

namespace FixIt.Mobile.Tests.ViewModels;

public class IssuesViewModelTests
{
    [Fact]
    public async Task LoadIssues_SetsIsLoading_ThenClearsIt()
    {
        var tcs = new TaskCompletionSource<List<Issue>>();
        var api = CreateApi();
        api.Setup(x => x.GetIssuesAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);
        var vm = CreateViewModel(api);

        var task = vm.LoadIssuesCommand.ExecuteAsync(null);
        vm.IsLoading.Should().BeTrue();
        tcs.SetResult([]);
        await task;

        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadIssues_WhenApiReturnsItems_PopulatesCollection()
    {
        var vm = CreateViewModelWithIssues([new Issue { Id = "1" }, new Issue { Id = "2" }]);

        await vm.LoadIssuesCommand.ExecuteAsync(null);

        vm.Issues.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadIssues_WhenApiReturnsEmpty_SetsIsEmpty()
    {
        var vm = CreateViewModelWithIssues([]);

        await vm.LoadIssuesCommand.ExecuteAsync(null);

        vm.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task SetFilter_TriggersReload_WithCorrectFilter()
    {
        string? capturedFilter = null;
        var api = CreateApi();
        api.Setup(x => x.GetIssuesAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string?, string?, int, int, CancellationToken>((filter, _, _, _, _) => capturedFilter = filter)
            .ReturnsAsync([]);
        var vm = CreateViewModel(api);

        await vm.SetFilterCommand.ExecuteAsync(AppConstants.FilterResolved);

        capturedFilter.Should().Be(AppConstants.FilterResolved);
        vm.IsResolvedFilterActive.Should().BeTrue();
    }

    [Fact]
    public async Task SearchText_Change_DebouncesThenReloads()
    {
        string? capturedSearch = null;
        var api = CreateApi();
        api.Setup(x => x.GetIssuesAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string?, string?, int, int, CancellationToken>((_, search, _, _, _) => capturedSearch = search)
            .ReturnsAsync([]);
        var vm = CreateViewModel(api);

        vm.SearchText = "pothole";
        await Task.Delay(MobileSettings.SearchDebounceMs + 150);

        capturedSearch.Should().Be("pothole");
    }

    private static IssuesViewModel CreateViewModelWithIssues(List<Issue> issues)
    {
        var api = CreateApi();
        api.Setup(x => x.GetIssuesAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(issues);
        return CreateViewModel(api);
    }

    private static IssuesViewModel CreateViewModel(Mock<IApiService> api)
    {
        return new IssuesViewModel(api.Object, new NoopAnalyticsService(), new NoopPerformanceService());
    }

    private static Mock<IApiService> CreateApi() => new();
}
