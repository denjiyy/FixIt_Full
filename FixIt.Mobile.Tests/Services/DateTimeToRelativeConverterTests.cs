using System.Globalization;
using FluentAssertions;
using FixIt.Mobile.Converters;
using Xunit;

namespace FixIt.Mobile.Tests.Services;

public class DateTimeToRelativeConverterTests
{
    private readonly DateTimeToRelativeConverter _converter = new();

    [Fact]
    public void Convert_JustNow_ReturnsCorrectString()
    {
        var result = _converter.Convert(DateTime.UtcNow.AddSeconds(-15), typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be("just now");
    }

    [Fact]
    public void Convert_OneHourAgo_ReturnsCorrectString()
    {
        var result = _converter.Convert(DateTime.UtcNow.AddHours(-1), typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be("1 hour ago");
    }

    [Fact]
    public void Convert_Yesterday_ReturnsCorrectString()
    {
        var result = _converter.Convert(DateTime.UtcNow.AddDays(-1), typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be("Yesterday");
    }

    [Fact]
    public void Convert_SevenDaysAgo_ReturnsCorrectString()
    {
        var date = DateTime.UtcNow.AddDays(-7);
        var result = _converter.Convert(date, typeof(string), null, CultureInfo.InvariantCulture);

        result.Should().Be(date.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture));
    }
}
