using FluentAssertions;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services.Contracts;
using FixIt.Mobile.Tests.TestSupport;
using FixIt.Mobile.ViewModels;
using Moq;
using Xunit;

namespace FixIt.Mobile.Tests.ViewModels;

public class HazardModeViewModelTests
{
    [Fact]
    public async Task Load_PopulatesHazards_AndClearsEmpty()
    {
        var vm = CreateViewModel(out _, nearby: new[] { Hazard("h1"), Hazard("h2") });

        await vm.LoadHazardsCommand.ExecuteAsync(null);

        vm.Hazards.Should().HaveCount(2);
        vm.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task Load_WithNoHazards_SetsEmpty()
    {
        var vm = CreateViewModel(out _);

        await vm.LoadHazardsCommand.ExecuteAsync(null);

        vm.Hazards.Should().BeEmpty();
        vm.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task Load_QueriesNearbyHazards_AtUserLocation()
    {
        var vm = CreateViewModel(out var api);
        vm.SetUserLocationForTesting(40.1, 28.2);

        await vm.LoadHazardsCommand.ExecuteAsync(null);

        api.Verify(x => x.GetNearbyHazardsAsync(40.1, 28.2, It.IsAny<double>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnAppearing_LoadsNearbyHazards()
    {
        var vm = CreateViewModel(out var api, nearby: new[] { Hazard("h1") });

        await vm.OnAppearingAsync();

        vm.Hazards.Should().ContainSingle();
        api.Verify(x => x.GetNearbyHazardsAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirm_IncrementsConfirmationCount()
    {
        var vm = CreateViewModel(out _, nearby: new[] { Hazard("h1", confirmations: 2) });
        await vm.LoadHazardsCommand.ExecuteAsync(null);

        await vm.ConfirmHazardCommand.ExecuteAsync("h1");

        vm.Hazards.Single().Confirmations.Should().Be(3);
    }

    [Fact]
    public async Task Confirm_WhenLoggedOut_DoesNothing()
    {
        var vm = CreateViewModel(out var api, loggedIn: false);

        await vm.ConfirmHazardCommand.ExecuteAsync("h1");

        api.Verify(x => x.ConfirmHazardAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MapPointSelected_WhenLoggedIn_OpensPanelAndGeocodes()
    {
        var vm = CreateViewModel(out var api);
        api.Setup(x => x.ReverseGeocodeAsync(42.7, 23.3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReverseGeocodeResult { Address = "Vitosha Blvd 1", CityId = "city-9" });

        await vm.OnMapPointSelectedAsync(42.7, 23.3, CancellationToken.None);

        vm.ReportLatitude.Should().Be(42.7);
        vm.ReportLongitude.Should().Be(23.3);
        vm.IsReportPanelVisible.Should().BeTrue();
        vm.ReportAddress.Should().Be("Vitosha Blvd 1");
        vm.CityId.Should().Be("city-9");
    }

    [Fact]
    public async Task MapPointSelected_WhenLoggedOut_KeepsPanelClosed_AndPromptsLogin()
    {
        var vm = CreateViewModel(out var api, loggedIn: false);

        await vm.OnMapPointSelectedAsync(42.7, 23.3, CancellationToken.None);

        vm.IsReportPanelVisible.Should().BeFalse();
        vm.HasError.Should().BeTrue();
        api.Verify(x => x.ReverseGeocodeAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void CanSubmitReport_FalseUntilAllRequiredFieldsPresent()
    {
        var vm = CreateViewModel(out _);
        vm.CanSubmitReport.Should().BeFalse();

        vm.ReportTitle = "Sinkhole";
        vm.ReportDescription = "Large sinkhole blocking the lane";
        vm.SelectTypeCommand.Execute(TypeOption(vm, "Pothole"));
        vm.CanSubmitReport.Should().BeFalse("coordinates are still missing");

        vm.ReportLatitude = 42.7;
        vm.ReportLongitude = 23.3;
        vm.CanSubmitReport.Should().BeTrue();
    }

    [Fact]
    public void CanSubmitReport_FalseWhenTypeNotChosen()
    {
        var vm = CreateViewModel(out _);
        vm.ReportTitle = "Sinkhole";
        vm.ReportDescription = "Large sinkhole blocking the lane";
        vm.ReportLatitude = 42.7;
        vm.ReportLongitude = 23.3;

        vm.CanSubmitReport.Should().BeFalse();
    }

    [Fact]
    public void SelectType_SetsKeyAndTogglesSelection()
    {
        var vm = CreateViewModel(out _);

        vm.SelectTypeCommand.Execute(TypeOption(vm, "Crime"));

        vm.SelectedTypeKey.Should().Be("Crime");
        vm.HazardTypes.Single(t => t.Key == "Crime").IsSelected.Should().BeTrue();
        vm.HazardTypes.Where(t => t.Key != "Crime").Should().OnlyContain(t => !t.IsSelected);
    }

    [Fact]
    public void SelectSeverity_UpdatesSelectedSeverityAndFlags()
    {
        var vm = CreateViewModel(out _);

        vm.SelectSeverityCommand.Execute("High");

        vm.SelectedSeverity.Should().Be("High");
        vm.IsSeverityHigh.Should().BeTrue();
        vm.IsSeverityMedium.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitReport_SendsFormToApi_WithCoordinatesTypeAndSeverity()
    {
        var vm = CreateViewModel(out var api);
        await vm.OnMapPointSelectedAsync(42.7, 23.3, CancellationToken.None);
        vm.SelectSeverityCommand.Execute("High");
        vm.SelectTypeCommand.Execute(TypeOption(vm, "Pothole"));
        vm.ReportTitle = "Sinkhole on 5th";
        vm.ReportDescription = "Large sinkhole blocking the road";

        await vm.SubmitReportCommand.ExecuteAsync(null);

        api.Verify(x => x.ReportHazardAsync(
            "Pothole", "High", "Sinkhole on 5th", "Large sinkhole blocking the road",
            42.7, 23.3, It.IsAny<string?>(), AppConstants.DefaultCityId, It.IsAny<CancellationToken>()), Times.Once);
        vm.ReportSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitReport_OnSuccess_ResetsFormAndReloads()
    {
        var vm = CreateViewModel(out var api);
        await vm.OnMapPointSelectedAsync(42.7, 23.3, CancellationToken.None);
        vm.SelectTypeCommand.Execute(TypeOption(vm, "Pothole"));
        vm.ReportTitle = "Sinkhole";
        vm.ReportDescription = "Large sinkhole blocking the road";

        await vm.SubmitReportCommand.ExecuteAsync(null);

        vm.IsReportPanelVisible.Should().BeFalse();
        vm.ReportTitle.Should().BeEmpty();
        vm.SelectedTypeKey.Should().BeEmpty();
        vm.ReportLatitude.Should().BeNull();
        // One nearby query for the post-submit reload.
        api.Verify(x => x.GetNearbyHazardsAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitReport_OnFailure_ShowsError_AndKeepsPanelOpen()
    {
        var vm = CreateViewModel(out _, reportResult: new ApiResult(false, "City ID is required"));
        await vm.OnMapPointSelectedAsync(42.7, 23.3, CancellationToken.None);
        vm.SelectTypeCommand.Execute(TypeOption(vm, "Pothole"));
        vm.ReportTitle = "Sinkhole";
        vm.ReportDescription = "Large sinkhole blocking the road";

        await vm.SubmitReportCommand.ExecuteAsync(null);

        vm.ReportError.Should().Be("City ID is required");
        vm.HasReportError.Should().BeTrue();
        vm.ReportSucceeded.Should().BeFalse();
        vm.IsReportPanelVisible.Should().BeTrue();
    }

    [Fact]
    public async Task CancelReport_HidesPanelAndClearsForm()
    {
        var vm = CreateViewModel(out _);
        await vm.OnMapPointSelectedAsync(42.7, 23.3, CancellationToken.None);
        vm.SelectTypeCommand.Execute(TypeOption(vm, "Pothole"));
        vm.ReportTitle = "Sinkhole";

        vm.CancelReportCommand.Execute(null);

        vm.IsReportPanelVisible.Should().BeFalse();
        vm.ReportTitle.Should().BeEmpty();
        vm.SelectedTypeKey.Should().BeEmpty();
    }

    private static HazardTypeOption TypeOption(HazardModeViewModel vm, string key) =>
        vm.HazardTypes.Single(t => t.Key == key);

    private static SafetyHazard Hazard(string id, int confirmations = 0) => new()
    {
        Id = id,
        Title = "Test hazard",
        Severity = "High",
        Latitude = 42.7,
        Longitude = 23.3,
        Confirmations = confirmations
    };

    private static HazardModeViewModel CreateViewModel(
        out Mock<IApiService> api,
        bool loggedIn = true,
        ApiResult? reportResult = null,
        IEnumerable<SafetyHazard>? nearby = null)
    {
        api = new Mock<IApiService>();
        api.Setup(x => x.GetNearbyHazardsAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((nearby ?? Array.Empty<SafetyHazard>()).ToList());
        api.Setup(x => x.ConfirmHazardAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult(true));
        api.Setup(x => x.ReportHazardAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportResult ?? new ApiResult(true));
        api.Setup(x => x.ReverseGeocodeAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReverseGeocodeResult?)null);

        var auth = new Mock<IAuthService>();
        auth.SetupGet(x => x.IsLoggedIn).Returns(loggedIn);

        return new HazardModeViewModel(api.Object, auth.Object, new NoopAnalyticsService(), new NoopPerformanceService());
    }
}
