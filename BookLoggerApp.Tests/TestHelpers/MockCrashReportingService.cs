using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Tests.TestHelpers;

public sealed class MockCrashReportingService : ICrashReportingService
{
    public List<(Exception Exception, IReadOnlyDictionary<string, string>? Keys)> NonFatals { get; } = new();
    public List<Exception> Fatals { get; } = new();
    public List<string> Logs { get; } = new();
    public Dictionary<string, string?> CustomKeys { get; } = new();
    public string? UserId { get; private set; }
    public bool? CollectionEnabled { get; private set; }

    public void RecordNonFatal(Exception exception, IReadOnlyDictionary<string, string>? customKeys = null)
        => NonFatals.Add((exception, customKeys));

    public void RecordFatal(Exception exception) => Fatals.Add(exception);

    public void Log(string message) => Logs.Add(message);

    public void SetUserId(string? userId) => UserId = userId;

    public void SetCustomKey(string key, string? value) => CustomKeys[key] = value;

    public void SetCrashlyticsCollectionEnabled(bool enabled) => CollectionEnabled = enabled;
}
