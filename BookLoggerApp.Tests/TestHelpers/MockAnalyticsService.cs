using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Tests.TestHelpers;

public sealed class MockAnalyticsService : IAnalyticsService
{
    public List<(string Name, IDictionary<string, object?>? Parameters)> LoggedEvents { get; } = new();
    public List<(string Screen, string? Class)> LoggedScreens { get; } = new();
    public Dictionary<string, string?> UserProperties { get; } = new();
    public string? UserId { get; private set; }
    public bool? CollectionEnabled { get; private set; }
    public int ResetCount { get; private set; }

    public void LogEvent(string name, IDictionary<string, object?>? parameters = null)
        => LoggedEvents.Add((name, parameters));

    public void LogScreenView(string screenName, string? screenClass = null)
        => LoggedScreens.Add((screenName, screenClass));

    public void SetUserProperty(string name, string? value) => UserProperties[name] = value;

    public void SetUserId(string? userId) => UserId = userId;

    public void SetAnalyticsCollectionEnabled(bool enabled) => CollectionEnabled = enabled;

    public void ResetAnalyticsData() => ResetCount++;
}
