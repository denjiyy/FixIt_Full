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
    public void CanSubmit_FalseWhenTitleEmpty()
    {
        var vm = CreateViewModel(new ApiResult(true), out _);
        vm.SetCoordinatesForTesting(42.7, 23.3);

        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void CanSubmit_FalseWhenNoCoordinates()
    {
        var vm = CreateViewModel(new ApiResult(true), out _);
        vm.Title = "Broken sidewalk";

        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void CanSubmit_FalseWhenLocationNotConfirmed()
    {
        var vm = CreateViewModel(new ApiResult(true), out _);
        vm.Title = "Broken sidewalk";
        vm.SetCoordinatesForTesting(42.7, 23.3, confirmed: false);

        vm.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void CanSubmit_TrueWithNoPhotoIfTitleAndConfirmedCoords()
    {
        var vm = CreateViewModel(new ApiResult(true), out _);
        vm.Title = "Broken sidewalk";
        vm.SetCoordinatesForTesting(42.7, 23.3);

        vm.CanSubmit.Should().BeTrue();
    }

    [Fact]
    public async Task Submit_SendsCoordinatesAndAddress_ToApi()
    {
        Shell.Current = new Shell();
        var vm = CreateViewModel(new ApiResult(true), out var api);
        vm.Title = "Broken sidewalk";
        vm.Address = "ул. Витоша 1";
        vm.CityId = "city-42";
        vm.SetCoordinatesForTesting(42.7, 23.3);

        await vm.SubmitCommand.ExecuteAsync(null);

        api.Verify(x => x.ReportIssueAsync(
            It.Is<ReportIssueRequest>(r =>
                r.Latitude == 42.7
                && r.Longitude == 23.3
                && r.CityId == "city-42"
                && r.Address == "ул. Витоша 1"
                && r.Title == "Broken sidewalk"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Submit_WithZeroPhotos_StillSucceeds()
    {
        Shell.Current = new Shell();
        var vm = CreateViewModel(new ApiResult(true), out _);
        vm.Title = "Broken sidewalk";
        vm.SetCoordinatesForTesting(42.7, 23.3);

        await vm.SubmitCommand.ExecuteAsync(null);

        vm.SubmissionSucceeded.Should().BeTrue();
        Shell.Current.LastRoute.Should().Be(AppConstants.RouteHome);
    }

    [Fact]
    public async Task Submit_WhenApiFails_ShowsError()
    {
        var vm = CreateViewModel(new ApiResult(false, "Upload failed"), out _);
        vm.Title = "Broken sidewalk";
        vm.SetCoordinatesForTesting(42.7, 23.3);

        await vm.SubmitCommand.ExecuteAsync(null);

        vm.SubmitError.Should().Be("Upload failed");
    }

    [Fact]
    public void AddPhotoForTesting_AppendsToPhotosCollection()
    {
        var vm = CreateViewModel(new ApiResult(true), out _);

        vm.AddPhotoForTesting(NewPhoto());
        vm.AddPhotoForTesting(NewPhoto());

        vm.Photos.Should().HaveCount(2);
        vm.HasPhotos.Should().BeTrue();
    }

    [Fact]
    public void RemovePhoto_RemovesItem()
    {
        var vm = CreateViewModel(new ApiResult(true), out _);
        var first = NewPhoto();
        var second = NewPhoto();
        vm.AddPhotoForTesting(first);
        vm.AddPhotoForTesting(second);

        vm.RemovePhotoCommand.Execute(first);

        vm.Photos.Should().ContainSingle().Which.Should().Be(second);
    }

    [Fact]
    public async Task OnMapPointSelected_UpdatesCoordsAndCallsReverseGeocode()
    {
        var vm = CreateViewModel(new ApiResult(true), out var api);
        api.Setup(x => x.ReverseGeocodeAsync(43.0, 24.0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReverseGeocodeResult
            {
                Address = "Test road 12",
                CityId = "city-7"
            });

        await vm.OnMapPointSelectedAsync(43.0, 24.0, CancellationToken.None);

        vm.Latitude.Should().Be(43.0);
        vm.Longitude.Should().Be(24.0);
        vm.IsLocationConfirmed.Should().BeTrue();
        vm.Address.Should().Be("Test road 12");
        vm.CityId.Should().Be("city-7");
        api.Verify(x => x.ReverseGeocodeAsync(43.0, 24.0, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ReportIssueViewModel CreateViewModel(ApiResult result, out Mock<IApiService> api)
    {
        api = new Mock<IApiService>();
        api.Setup(x => x.ReportIssueAsync(It.IsAny<ReportIssueRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

        var auth = new Mock<IAuthService>();
        auth.SetupGet(x => x.IsLoggedIn).Returns(true);

        return new ReportIssueViewModel(api.Object, auth.Object, new NoopAnalyticsService(), new NoopPerformanceService());
    }

    private static PhotoAttachment NewPhoto() => new()
    {
        Bytes = new byte[] { 1, 2, 3 },
        FileName = "photo.jpg",
        ContentType = "image/jpeg"
    };
}
