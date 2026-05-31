using Microsoft.Extensions.Configuration;

namespace FixIt.Services.AI;

/// <summary>
/// Canonical reader for OpenAI configuration, shared by every AI service so they
/// agree on whether the API is usable. The API counts as "configured" only when
/// it is enabled (<c>OpenAI:Enabled</c>, default true) AND a non-placeholder key
/// is present. When it isn't, services fall back to local heuristics.
/// </summary>
public static class OpenAiConfig
{
    public const string DefaultModel = "gpt-4o-mini";
    private const int DefaultTimeoutSeconds = 30;

    public static string? GetApiKey(IConfiguration configuration)
        => configuration["OpenAI:ApiKey"]?.Trim();

    public static bool IsEnabled(IConfiguration configuration)
        => configuration.GetValue("OpenAI:Enabled", true);

    public static string GetModel(IConfiguration configuration)
    {
        var model = configuration["OpenAI:Model"]?.Trim();
        return string.IsNullOrEmpty(model) ? DefaultModel : model;
    }

    public static int GetTimeoutSeconds(IConfiguration configuration)
        => int.TryParse(configuration["OpenAI:TimeoutSeconds"], out var seconds) && seconds > 0
            ? seconds
            : DefaultTimeoutSeconds;

    /// <summary>True when AI is enabled and a usable (non-placeholder) key is present.</summary>
    public static bool IsConfigured(IConfiguration configuration)
        => IsEnabled(configuration) && IsUsableKey(GetApiKey(configuration));

    /// <summary>
    /// A key is usable if it is non-empty and not an obvious placeholder. The
    /// <c>sk-</c> prefix is intentionally NOT required, so OpenAI-compatible proxy
    /// or gateway keys (Azure OpenAI, OpenRouter, LiteLLM, …) work too.
    /// </summary>
    public static bool IsUsableKey(string? apiKey)
        => !string.IsNullOrWhiteSpace(apiKey)
           && !apiKey.Contains("YOUR", StringComparison.OrdinalIgnoreCase)
           && !apiKey.StartsWith("${", StringComparison.Ordinal)
           && !apiKey.Equals("changeme", StringComparison.OrdinalIgnoreCase);
}
