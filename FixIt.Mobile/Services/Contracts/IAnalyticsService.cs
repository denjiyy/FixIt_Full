namespace FixIt.Mobile.Services.Contracts;

public interface IAnalyticsService
{
    Task TrackEvent(string eventName, Dictionary<string, string>? properties = null);
    Task TrackError(Exception ex, Dictionary<string, string>? properties = null);
    Task TrackScreen(string screenName);
}
