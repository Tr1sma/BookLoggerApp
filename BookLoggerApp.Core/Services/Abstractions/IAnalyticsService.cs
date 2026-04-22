namespace BookLoggerApp.Core.Services.Abstractions;

public interface IAnalyticsService
{
    void LogEvent(string name, IDictionary<string, object?>? parameters = null);

    void LogScreenView(string screenName, string? screenClass = null);

    void SetUserProperty(string name, string? value);

    void SetUserId(string? userId);

    void SetAnalyticsCollectionEnabled(bool enabled);

    void ResetAnalyticsData();
}
