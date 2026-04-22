#if ANDROID
using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace BookLoggerApp.Platforms.AndroidImpl.Analytics;

[ProviderAlias("Crashlytics")]
public sealed class CrashlyticsLoggerProvider : ILoggerProvider
{
    private readonly ICrashReportingService _crash;
    private readonly IAnalyticsConsentGate _gate;
    private readonly ConcurrentDictionary<string, CrashlyticsLogger> _loggers = new();
    private bool _disposed;

    public CrashlyticsLoggerProvider(ICrashReportingService crash, IAnalyticsConsentGate gate)
    {
        _crash = crash;
        _gate = gate;
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new CrashlyticsLogger(name, _crash, _gate));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loggers.Clear();
    }
}

public sealed class CrashlyticsLogger : ILogger
{
    private static readonly HashSet<string> RedactedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "bookTitle", "book_title", "title", "isbn", "author", "quote", "quote_text",
        "annotation", "annotation_text", "note", "notes", "shelfName", "shelf_name",
        "plantNickname", "plant_nickname", "email", "user_name", "username",
        "password", "token", "secret"
    };

    private readonly string _category;
    private readonly ICrashReportingService _crash;
    private readonly IAnalyticsConsentGate _gate;

    public CrashlyticsLogger(string category, ICrashReportingService crash, IAnalyticsConsentGate gate)
    {
        _category = category;
        _crash = crash;
        _gate = gate;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        if (!_gate.CrashAllowed) return;

        try
        {
            var msg = formatter(state, exception);
            if (string.IsNullOrEmpty(msg) && exception is null) return;

            var sanitized = Sanitize(state, msg);
            var prefix = logLevel switch
            {
                LogLevel.Critical => "C",
                LogLevel.Error => "E",
                LogLevel.Warning => "W",
                _ => "I"
            };
            _crash.Log($"[{prefix}][{_category}] {sanitized}");

            if (exception is not null && logLevel >= LogLevel.Error)
            {
                _crash.RecordNonFatal(exception, new Dictionary<string, string>
                {
                    ["source"] = "ilogger",
                    ["category"] = _category,
                    ["level"] = logLevel.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CrashlyticsLogger.Log failed: {ex}");
        }
    }

    private static string Sanitize<TState>(TState state, string formatted)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> kvps)
        {
            foreach (var kvp in kvps)
            {
                if (RedactedKeys.Contains(kvp.Key) && !string.IsNullOrEmpty(formatted))
                {
                    var value = kvp.Value?.ToString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        formatted = formatted.Replace(value, "[redacted]", StringComparison.Ordinal);
                    }
                }
            }
        }
        return formatted.Length > 512 ? formatted.Substring(0, 512) + "…" : formatted;
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
#endif
