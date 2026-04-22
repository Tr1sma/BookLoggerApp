using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.Services.Analytics;

public sealed class NoOpCrashReportingService : ICrashReportingService
{
    public static readonly NoOpCrashReportingService Instance = new();

    public void RecordNonFatal(Exception exception, IReadOnlyDictionary<string, string>? customKeys = null) { }

    public void RecordFatal(Exception exception) { }

    public void Log(string message) { }

    public void SetUserId(string? userId) { }

    public void SetCustomKey(string key, string? value) { }

    public void SetCrashlyticsCollectionEnabled(bool enabled) { }
}
