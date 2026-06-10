using System.Net;
using System.Net.Http.Json;
using MongoDB.Bson;
using Xunit;

namespace FixIt.Tests.Integration;

/// <summary>
/// End-to-end coverage for the Safety/hazard HTTP surface against the real
/// pipeline (WebApplicationFactory + Mongo testcontainer): report → fetch →
/// list nearby → confirm → resolve → delete → restore, plus the auth and
/// validation gates. This is also the exact contract the FixIt.Mobile hazard
/// reporting flow now depends on (POST /api/safety/hazards), so it doubles as a
/// web↔mobile contract guard. Each test isolates itself with a fresh (GUID)
/// city id and freshly provisioned users so they don't cross-contaminate.
/// </summary>
public class SafetyHazardIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public SafetyHazardIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private void RequireDocker() => Skip.IfNot(_fixture.IsAvailable,
        $"Docker testcontainer unavailable. {_fixture.UnavailabilityReason ?? "(no reason captured)"}");

    [SkippableFact]
    public async Task ReportHazard_AsAuthenticatedUser_PersistsAndReturns201()
    {
        RequireDocker();
        var (_, client) = await NewUserClientAsync();
        var cityId = NewCityId();

        var response = await client.PostAsJsonAsync("/api/safety/hazards", new
        {
            type = "Pothole",
            severity = "High",
            title = "Sinkhole on Vitosha Blvd",
            description = "Large sinkhole blocking the right lane near the tram stop.",
            latitude = 42.6977,
            longitude = 23.3219,
            address = "Vitosha Blvd 1",
            cityId,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var hazard = await ReadDataAsync<HazardDetailDto>(response);
        Assert.False(string.IsNullOrEmpty(hazard.Id));
        Assert.Equal("Pothole", hazard.Type);
        Assert.Equal("High", hazard.Severity);
        Assert.Equal("Sinkhole on Vitosha Blvd", hazard.Title);
    }

    [SkippableFact]
    public async Task ReportHazard_WithoutAuthentication_Returns401()
    {
        RequireDocker();
        var client = _fixture.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.PostAsJsonAsync("/api/safety/hazards", new
        {
            type = "Pothole",
            severity = "High",
            title = "Unauthorized report",
            description = "Should never be stored because the caller is anonymous.",
            latitude = 42.6977,
            longitude = 23.3219,
            cityId = NewCityId(),
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task ReportHazard_WithInvalidCoordinates_Returns400()
    {
        RequireDocker();
        var (_, client) = await NewUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/safety/hazards", new
        {
            type = "Pothole",
            severity = "High",
            title = "Bad coordinates",
            description = "Latitude is wildly out of range and must be rejected.",
            latitude = 999.0,
            longitude = 23.3219,
            cityId = NewCityId(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task ReportHazard_WithInvalidType_Returns400()
    {
        RequireDocker();
        var (_, client) = await NewUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/safety/hazards", new
        {
            type = "NotARealHazardType",
            severity = "High",
            title = "Bad type",
            description = "The hazard type does not map to the HazardType enum.",
            latitude = 42.6977,
            longitude = 23.3219,
            cityId = NewCityId(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task CreateHazard_WithoutCity_AndNoPreferredCity_Returns400()
    {
        RequireDocker();
        var (_, client) = await NewUserClientAsync();

        // No cityId in the payload and the freshly provisioned user has no
        // PreferredCityId, so the mobile-friendly endpoint must reject it.
        var response = await client.PostAsJsonAsync("/api/safety/hazards", new
        {
            type = "Pothole",
            severity = "High",
            title = "No city",
            description = "City id is omitted and there is no preferred city to fall back to.",
            latitude = 42.6977,
            longitude = 23.3219,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task GetHazardById_AfterReport_ReturnsTheStoredHazard()
    {
        RequireDocker();
        var (_, client) = await NewUserClientAsync();
        var (hazardId, _) = await ReportHazardAsync(client);

        var response = await client.GetAsync($"/api/safety/{hazardId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var hazard = await ReadDataAsync<HazardDetailDto>(response);
        Assert.Equal(hazardId, hazard.Id);
        Assert.Equal("Pothole", hazard.Type);
        Assert.Equal("High", hazard.Severity);
        Assert.Equal("Sinkhole on Vitosha Blvd", hazard.Title);
        Assert.False(hazard.IsResolved);
    }

    [SkippableFact]
    public async Task NearbyHazards_IncludesReportedHazard()
    {
        RequireDocker();
        var (_, client) = await NewUserClientAsync();
        var (hazardId, cityId) = await ReportHazardAsync(client);

        var response = await client.GetAsync(
            $"/api/safety/nearby-hazards?cityId={cityId}&latitude=42.6977&longitude=23.3219&radiusKm=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var hazards = await ReadDataAsync<List<HazardAlertDto>>(response);
        Assert.Contains(hazards, h => h.Id == hazardId);
    }

    [SkippableFact]
    public async Task ConfirmHazard_IncrementsCount_AndIsIdempotentPerUser()
    {
        RequireDocker();
        var (_, reporter) = await NewUserClientAsync();
        var (hazardId, _) = await ReportHazardAsync(reporter);

        // A different user confirms; a second confirm by the same user must not
        // double-count (ConfirmedByUserIds is a set).
        var (_, confirmer) = await NewUserClientAsync();
        var first = await confirmer.PostAsync($"/api/safety/{hazardId}/confirm", null);
        var second = await confirmer.PostAsync($"/api/safety/{hazardId}/confirm", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var confirmation = await ReadDataAsync<ConfirmationDto>(second);
        Assert.Equal(1, confirmation.Confirmations);
    }

    [SkippableFact]
    public async Task ResolveHazard_AsNonAdmin_IsForbidden_AndLeavesHazardActive()
    {
        RequireDocker();
        var (_, client) = await NewUserClientAsync();
        var (hazardId, _) = await ReportHazardAsync(client);

        var response = await client.PostAsJsonAsync($"/api/safety/{hazardId}/resolve", new { notes = "Trying as non-admin" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // The forbidden attempt must not have changed state.
        var get = await client.GetAsync($"/api/safety/{hazardId}");
        var hazard = await ReadDataAsync<HazardDetailDto>(get);
        Assert.False(hazard.IsResolved);
    }

    [SkippableFact]
    public async Task ResolveHazard_AsAdmin_MarksResolved()
    {
        RequireDocker();
        var (_, reporter) = await NewUserClientAsync();
        var (hazardId, _) = await ReportHazardAsync(reporter);
        var admin = await NewAdminClientAsync();

        var response = await admin.PostAsJsonAsync($"/api/safety/{hazardId}/resolve", new { notes = "Crew dispatched" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var hazard = await ReadDataAsync<HazardDetailDto>(response);
        Assert.True(hazard.IsResolved);
    }

    [SkippableFact]
    public async Task DeleteHazard_AsReporter_ThenGet_Returns404()
    {
        RequireDocker();
        var (_, client) = await NewUserClientAsync();
        var (hazardId, _) = await ReportHazardAsync(client);

        var delete = await client.DeleteAsync($"/api/safety/{hazardId}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var get = await client.GetAsync($"/api/safety/{hazardId}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [SkippableFact]
    public async Task RestoreHazard_AsAdmin_AfterDelete_MakesItFetchableAgain()
    {
        RequireDocker();
        var (_, reporter) = await NewUserClientAsync();
        var (hazardId, _) = await ReportHazardAsync(reporter);
        await reporter.DeleteAsync($"/api/safety/{hazardId}");

        var admin = await NewAdminClientAsync();
        var restore = await admin.PostAsync($"/api/safety/{hazardId}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        var get = await reporter.GetAsync($"/api/safety/{hazardId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [SkippableFact]
    public async Task CityStatistics_ReflectReportedHazard()
    {
        RequireDocker();
        var (_, client) = await NewUserClientAsync();
        var (_, cityId) = await ReportHazardAsync(client, severity: "Critical");

        var response = await client.GetAsync($"/api/safety/city/{cityId}/statistics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stats = await ReadDataAsync<StatisticsDto>(response);
        Assert.Equal(1, stats.TotalHazards);
        Assert.Equal(1, stats.CriticalHazards);
    }

    // ---- helpers ----

    private static string NewCityId() => ObjectId.GenerateNewId().ToString();

    private async Task<(string Email, HttpClient Client)> NewUserClientAsync()
    {
        var (email, password) = await _fixture.ProvisionRegularUserAsync(
            email: $"hazard-user-{Guid.NewGuid():N}@fixit.test");
        var (_, client) = await _fixture.LoginAndGetClientAsync(email, password);
        return (email, client);
    }

    private async Task<HttpClient> NewAdminClientAsync()
    {
        var (email, password) = await _fixture.ProvisionAdminAsync(
            email: $"hazard-admin-{Guid.NewGuid():N}@fixit.test");
        var (_, client) = await _fixture.LoginAndGetClientAsync(email, password);
        return client;
    }

    private static async Task<(string HazardId, string CityId)> ReportHazardAsync(
        HttpClient client, string severity = "High")
    {
        var cityId = NewCityId();
        var response = await client.PostAsJsonAsync("/api/safety/hazards", new
        {
            type = "Pothole",
            severity,
            title = "Sinkhole on Vitosha Blvd",
            description = "Large sinkhole blocking the right lane near the tram stop.",
            latitude = 42.6977,
            longitude = 23.3219,
            address = "Vitosha Blvd 1",
            cityId,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var hazard = await ReadDataAsync<HazardDetailDto>(response);
        return (hazard.Id, cityId);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<IntegrationTestFixture.ApiEnvelope<T>>()
            ?? throw new InvalidOperationException("Response body was not a parseable ApiResponse envelope.");
        Assert.True(envelope.Success, $"Expected a success envelope but got: {envelope.Message}");
        return envelope.Data ?? throw new InvalidOperationException("Envelope contained no data.");
    }

    private sealed record HazardDetailDto(
        string Id, string Type, string Severity, string Title, string Description,
        double Latitude, double Longitude, string Address, int Confirmations,
        bool IsResolved, bool CanEdit, bool CanDelete, bool CanRestore);

    private sealed record HazardAlertDto(
        string Id, string Severity, double Latitude, double Longitude, int Confirmations);

    private sealed record ConfirmationDto(string HazardId, int Confirmations, bool IsResolved);

    private sealed record StatisticsDto(int TotalHazards, int CriticalHazards, int HighHazards);
}
