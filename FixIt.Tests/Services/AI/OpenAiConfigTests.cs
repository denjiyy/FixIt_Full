using FixIt.Services.AI;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FixIt.Tests.Services.AI;

/// <summary>
/// Locks in the single, canonical "is OpenAI configured?" rule now shared by both
/// AI services — replacing the two inconsistent checks that used to disagree.
/// </summary>
public class OpenAiConfigTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] pairs)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in pairs)
        {
            dict[key] = value;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void IsConfigured_EnabledWithRealKey_IsTrue()
    {
        Assert.True(OpenAiConfig.IsConfigured(Config(("OpenAI:ApiKey", "test-valid-api-key"), ("OpenAI:Enabled", "true"))));
    }

    [Fact]
    public void IsConfigured_EnabledDefaultsTrue_WhenUnset()
    {
        Assert.True(OpenAiConfig.IsConfigured(Config(("OpenAI:ApiKey", "test-valid-api-key"))));
    }

    [Fact]
    public void IsConfigured_DisabledWithRealKey_IsFalse()
    {
        // Previously the civic service ignored OpenAI:Enabled — this is the reconciliation.
        Assert.False(OpenAiConfig.IsConfigured(Config(("OpenAI:ApiKey", "test-valid-api-key"), ("OpenAI:Enabled", "false"))));
    }

    [Fact]
    public void IsConfigured_ProxyKeyWithoutSkPrefix_IsTrue()
    {
        // Previously the civic service hard-required an "sk-" prefix, rejecting valid
        // Azure/OpenRouter/gateway keys. The shared rule is lenient on the prefix.
        Assert.True(OpenAiConfig.IsConfigured(Config(("OpenAI:ApiKey", "azure-gateway-key-123"), ("OpenAI:Enabled", "true"))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("YOUR_KEY_HERE")]
    [InlineData("${OPENAI_API_KEY}")]
    [InlineData("changeme")]
    public void IsConfigured_EmptyOrPlaceholderKey_IsFalse(string key)
    {
        Assert.False(OpenAiConfig.IsConfigured(Config(("OpenAI:ApiKey", key), ("OpenAI:Enabled", "true"))));
    }

    [Fact]
    public void IsConfigured_MissingKey_IsFalse()
    {
        Assert.False(OpenAiConfig.IsConfigured(Config(("OpenAI:Enabled", "true"))));
    }

    [Fact]
    public void IsUsableKey_RejectsNullAndPlaceholders()
    {
        Assert.False(OpenAiConfig.IsUsableKey(null));
        Assert.False(OpenAiConfig.IsUsableKey("YOUR_KEY_HERE"));
        Assert.True(OpenAiConfig.IsUsableKey("valid-api-key-123"));
    }

    [Fact]
    public void GetModel_DefaultsWhenMissingOrEmpty()
    {
        Assert.Equal(OpenAiConfig.DefaultModel, OpenAiConfig.GetModel(Config()));
        Assert.Equal(OpenAiConfig.DefaultModel, OpenAiConfig.GetModel(Config(("OpenAI:Model", "  "))));
        Assert.Equal("gpt-4o", OpenAiConfig.GetModel(Config(("OpenAI:Model", "gpt-4o"))));
    }

    [Theory]
    [InlineData(null, 30)]
    [InlineData("0", 30)]
    [InlineData("notanumber", 30)]
    [InlineData("45", 45)]
    public void GetTimeoutSeconds_ParsesOrDefaults(string? value, int expected)
    {
        Assert.Equal(expected, OpenAiConfig.GetTimeoutSeconds(Config(("OpenAI:TimeoutSeconds", value))));
    }
}
