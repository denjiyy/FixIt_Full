using FluentAssertions;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services.Contracts;
using FixIt.Mobile.Tests.TestSupport;
using FixIt.Mobile.ViewModels;
using Moq;
using Xunit;

namespace FixIt.Mobile.Tests.ViewModels;

public class ReportIssueViewModelTests
{
    [Fact]
    public void CanSubmit_FalseWhenNoPhoto()
    {
        var vm = CreateViewModel(new ApiResult(true));
        vm.Title = "Broken sidewalk";

        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void CanSubmit_FalseWhenTitleEmpty()
    {
        var vm = CreateViewModel(new ApiResult(true));
        vm.SetPhotoForTesting([1, 2, 3]);

        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void CanSubmit_TrueWhenPhotoAndTitleSet()
    {
        var vm = CreateViewModel(new ApiResult(true));
        vm.SetPhotoForTesting([1, 2, 3]);
        vm.Title = "Broken sidewalk";

        vm.CanSubmit.Should().BeTrue();
    }

    [Fact]
    public async Task Submit_WhenApiSucceeds_NavigatesToHome()
    {
        Shell.Current = new Shell();
        var vm = CreateViewModel(new ApiResult(true));
        vm.SetPhotoForTesting([1, 2, 3]);
        vm.Title = "Broken sidewalk";

        await vm.SubmitCommand.ExecuteAsync(null);

        Shell.Current.LastRoute.Should().Be(AppConstants.RouteHome);
    }

    [Fact]
    public async Task Submit_WhenApiFails_ShowsError()
    {
        var vm = CreateViewModel(new ApiResult(false, "Upload failed"));
        vm.SetPhotoForTesting([1, 2, 3]);
        vm.Title = "Broken sidewalk";

        await vm.SubmitCommand.ExecuteAsync(null);

        vm.SubmitError.Should().Be("Upload failed");
    }

    private static ReportIssueViewModel CreateViewModel(ApiResult result)
    {
        var api = new Mock<IApiService>();
        api.Setup(x => x.ReportIssueAsync(It.IsAny<ReportIssueRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

        var auth = new Mock<IAuthService>();
        auth.SetupGet(x => x.IsLoggedIn).Returns(true);

        return new ReportIssueViewModel(api.Object, auth.Object, new NoopAnalyticsService(), new NoopPerformanceService());
    }
}
