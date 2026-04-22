namespace BookLoggerApp.Core.Services.Abstractions;

public interface ICrashReportingService
{
    void RecordNonFatal(Exception exception, IReadOnlyDictionary<string, string>? customKeys = null);

    void RecordFatal(Exception exception);

    void Log(string message);

    void SetUserId(string? userId);

    void SetCustomKey(string key, string? value);

    void SetCrashlyticsCollectionEnabled(bool enabled);
}
