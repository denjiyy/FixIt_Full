using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.Services;

public sealed class ConsoleAnalyticsService : IAnalyticsService
{
    public Task TrackEvent(string eventName, Dictionary<string, string>? properties = null)
    {
        Console.WriteLine($"[Analytics] Event: {eventName}{FormatProperties(properties)}");
        return Task.CompletedTask;
    }

    public Task TrackError(Exception ex, Dictionary<string, string>? properties = null)
    {
        Console.WriteLine($"[Analytics] Error: {ex.GetType().Name}: {ex.Message}{FormatProperties(properties)}");
        return Task.CompletedTask;
    }

    public Task TrackScreen(string screenName)
    {
        Console.WriteLine($"[Analytics] Screen: {screenName}");
        return Task.CompletedTask;
    }

    private static string FormatProperties(Dictionary<string, string>? properties)
    {
        if (properties == null || properties.Count == 0)
        {
            return string.Empty;
        }

        return " | " + string.Join(", ", properties.Select(p => $"{p.Key}={p.Value}"));
    }
}
