using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.Services.Analytics;

public sealed class NoOpAnalyticsService : IAnalyticsService
{
    public static readonly NoOpAnalyticsService Instance = new();

    public void LogEvent(string name, IDictionary<string, object?>? parameters = null) { }

    public void LogScreenView(string screenName, string? screenClass = null) { }

    public void SetUserProperty(string name, string? value) { }

    public void SetUserId(string? userId) { }

    public void SetAnalyticsCollectionEnabled(bool enabled) { }

    public void ResetAnalyticsData() { }
}
