using Microsoft.Extensions.Configuration;

namespace FixIt.Configuration;

/// <summary>
/// Startup-time configuration helpers focused on production safety.
/// </summary>
public static class StartupConfiguration
{
    private static readonly string[] PlaceholderMarkers =
    {
        "${",
        "your_",
        "your-",
        "<",
        ">",
        "change-me",
        "set-in-secret-manager",
        "placeholder",
        "example",
        "replace-me"
    };

    public static bool IsConfiguredSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        var lowered = trimmed.ToLowerInvariant();

        return !PlaceholderMarkers.Any(marker => lowered.Contains(marker, StringComparison.Ordinal));
    }

    public static string[] ResolveCorsOrigins(IConfiguration configuration, IEnumerable<string> fallbackOrigins)
    {
        var section = configuration.GetSection("Security:CorsAllowedOrigins");
        var arrayValues = section.Get<string[]>();
        if (arrayValues is { Length: > 0 })
        {
            var normalizedArray = NormalizeOrigins(arrayValues);
            if (normalizedArray.Length > 0)
            {
                return normalizedArray;
            }
        }

        var raw = configuration["Security:CorsAllowedOrigins"];
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var normalizedDelimited = NormalizeOrigins(raw.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            if (normalizedDelimited.Length > 0)
            {
                return normalizedDelimited;
            }
        }

        return NormalizeOrigins(fallbackOrigins);
    }

    private static string[] NormalizeOrigins(IEnumerable<string> origins)
    {
        return origins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
