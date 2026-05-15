using FluentAssertions;
using FixIt.Mobile.Localization;
using Xunit;

namespace FixIt.Mobile.Tests.Services;

public class LocalizationServiceTests
{
    [Fact]
    public void Get_EnglishKey_ReturnsEnglishString()
    {
        LocalizationService.SetCulture("en-US");

        LocalizationService.Get("Home_ReportCTA").Should().Be("Report an Issue");
    }

    [Fact]
    public void Get_BulgarianCulture_ReturnsBulgarianString()
    {
        LocalizationService.SetCulture("bg-BG");

        LocalizationService.Get("Home_ReportCTA").Should().Be("Подай сигнал");
    }

    [Fact]
    public void Get_UnknownKey_ReturnsKeyName()
    {
        LocalizationService.SetCulture("en-US");

        LocalizationService.Get("Missing_Key_For_Test").Should().Be("Missing_Key_For_Test");
    }
}
