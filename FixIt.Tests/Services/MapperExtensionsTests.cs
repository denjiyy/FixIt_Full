using FixIt.Models.Common;
using FixIt.Models.Issues;
using FixIt.ViewModels;
using MongoDB.Driver.GeoJsonObjectModel;
using Xunit;

namespace FixIt.Tests.Services;

public class MapperExtensionsTests
{
    [Fact]
    public void ToDetailResponse_MapsCoordinatesFromLocation()
    {
        var issue = NewIssue(longitude: 23.3219, latitude: 42.6977, address: "Vitosha 1");

        var response = issue.ToDetailResponse();

        Assert.Equal(23.3219, response.Longitude);
        Assert.Equal(42.6977, response.Latitude);
        Assert.Equal("Vitosha 1", response.Address);
    }

    [Fact]
    public void ToSummaryResponse_MapsCoordinatesFromLocation()
    {
        var issue = NewIssue(longitude: -118.2437, latitude: 34.0522);

        var summary = issue.ToSummaryResponse();

        Assert.Equal(-118.2437, summary.Longitude);
        Assert.Equal(34.0522, summary.Latitude);
    }

    [Fact]
    public void ToDetailResponse_WithNullLocation_ReturnsZeroCoordinates()
    {
        var issue = new Issue
        {
            Id = "i1",
            Title = "T",
            Description = "D",
            CityId = "c1",
            Reporter = new UserSummary { Id = "u1", DisplayName = "U" },
            Location = null!
        };

        var response = issue.ToDetailResponse();

        Assert.Equal(0, response.Longitude);
        Assert.Equal(0, response.Latitude);
    }

    private static Issue NewIssue(double longitude, double latitude, string? address = null) => new()
    {
        Id = "i1",
        Title = "T",
        Description = "D",
        CityId = "c1",
        Reporter = new UserSummary { Id = "u1", DisplayName = "U" },
        Location = GeoJson.Point(GeoJson.Geographic(longitude, latitude)),
        Address = address
    };
}
