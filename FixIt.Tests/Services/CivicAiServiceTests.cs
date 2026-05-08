using FixIt.Models.Enums;
using FixIt.Services.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FixIt.Tests.Services;

public class CivicAiServiceTests
{
    private static OpenAiCivicAiService CreateService()
    {
        var configValues = new Dictionary<string, string?>
        {
            ["OpenAI:ApiKey"] = string.Empty,
            ["OpenAI:Enabled"] = "true",
            ["OpenAI:Model"] = "gpt-4o-mini",
            ["OpenAI:TimeoutSeconds"] = "5"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        return new OpenAiCivicAiService(
            new HttpClient(),
            configuration,
            NullLogger<OpenAiCivicAiService>.Instance);
    }

    [Fact]
    public async Task SuggestIssueDraftAsync_WithoutApiKey_UsesFallbackClassification()
    {
        var service = CreateService();

        var result = await service.SuggestIssueDraftAsync(new IssueDraftSuggestionInput
        {
            Title = "Massive pothole near school crossing",
            Description = "The road damage is dangerous and urgent for pedestrians."
        });

        Assert.True(result.FallbackUsed);
        Assert.NotNull(result.Category);
        Assert.Equal(IssuePriority.Critical, result.Priority);
        Assert.False(string.IsNullOrWhiteSpace(result.Department));
    }

    [Fact]
    public async Task TranslateIssueFilterAsync_WithoutApiKey_UsesKeywordFallback()
    {
        var service = CreateService();

        var result = await service.TranslateIssueFilterAsync(new IssueFilterTranslationInput
        {
            Query = "critical road issues from last 7 days"
        });

        Assert.True(result.FallbackUsed);
        Assert.Equal(3, result.Priority);
        Assert.Equal("Infrastructure", result.Category);
        Assert.False(string.IsNullOrWhiteSpace(result.From));
        Assert.False(string.IsNullOrWhiteSpace(result.To));
    }
}
