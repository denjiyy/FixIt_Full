using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.Tests.TestSupport;

public sealed class NoopAnalyticsService : IAnalyticsService
{
    public List<string> Events { get; } = [];

    public Task TrackEvent(string eventName, Dictionary<string, string>? properties = null)
    {
        Events.Add(eventName);
        return Task.CompletedTask;
    }

    public Task TrackError(Exception ex, Dictionary<string, string>? properties = null)
    {
        Events.Add($"error:{ex.GetType().Name}");
        return Task.CompletedTask;
    }

    public Task TrackScreen(string screenName)
    {
        Events.Add($"screen:{screenName}");
        return Task.CompletedTask;
    }
}

public sealed class NoopPerformanceService : IPerformanceService
{
    public IDisposable StartTrace(string name)
    {
        return new NoopTrace();
    }

    private sealed class NoopTrace : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
