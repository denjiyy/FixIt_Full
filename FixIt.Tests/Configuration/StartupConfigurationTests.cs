using FixIt.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FixIt.Tests.Configuration;

public class StartupConfigurationTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("${JWT_SECRET_KEY}", false)]
    [InlineData("your-secret-here", false)]
    [InlineData("<generate-64-char-random-secret>", false)]
    [InlineData("change-me-local-jwt-secret-key", false)]
    [InlineData("replace-me", false)]
    [InlineData("super-long-real-secret-value-1234567890", true)]
    public void IsConfiguredSecret_ReturnsExpectedValue(string? input, bool expected)
    {
        var isConfigured = StartupConfiguration.IsConfiguredSecret(input);
        Assert.Equal(expected, isConfigured);
    }

    [Fact]
    public void ResolveCorsOrigins_UsesArrayValues_WhenProvided()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Security:CorsAllowedOrigins:0"] = "https://app.fixit.com",
            ["Security:CorsAllowedOrigins:1"] = "https://api.fixit.com/"
        });

        var result = StartupConfiguration.ResolveCorsOrigins(config, new[] { "http://localhost:5092" });

        Assert.True(result.SequenceEqual(new[] { "https://app.fixit.com", "https://api.fixit.com" }));
    }

    [Fact]
    public void ResolveCorsOrigins_ParsesDelimitedFallback_WhenArrayNotPresent()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Security:CorsAllowedOrigins"] = "https://app.fixit.com, https://api.fixit.com ; https://app.fixit.com/"
        });

        var result = StartupConfiguration.ResolveCorsOrigins(config, new[] { "http://localhost:5092" });

        Assert.True(result.SequenceEqual(new[] { "https://app.fixit.com", "https://api.fixit.com" }));
    }

    [Fact]
    public void ResolveCorsOrigins_UsesFallback_WhenConfigMissing()
    {
        var config = BuildConfig(new Dictionary<string, string?>());
        var result = StartupConfiguration.ResolveCorsOrigins(config, new[] { "http://localhost:5092", "http://localhost:5092/" });

        Assert.True(result.SequenceEqual(new[] { "http://localhost:5092" }));
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
