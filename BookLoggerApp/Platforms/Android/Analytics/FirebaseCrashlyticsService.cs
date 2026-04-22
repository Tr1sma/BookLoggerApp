#if ANDROID
using BookLoggerApp.Core.Services.Abstractions;
using Firebase.Crashlytics;

namespace BookLoggerApp.Platforms.AndroidImpl.Analytics;

public sealed class FirebaseCrashlyticsService : ICrashReportingService, IDisposable
{
    private readonly IAnalyticsConsentGate _gate;
    private readonly FirebaseCrashlytics _crashlytics;
    private bool _disposed;

    public FirebaseCrashlyticsService(IAnalyticsConsentGate gate)
    {
        _gate = gate;
        _crashlytics = FirebaseCrashlytics.Instance;
        _gate.ConsentChanged += OnConsentChanged;
    }

    public void RecordNonFatal(Exception exception, IReadOnlyDictionary<string, string>? customKeys = null)
    {
        if (!_gate.CrashAllowed) return;
        try
        {
            if (customKeys is not null)
            {
                foreach (var kvp in customKeys)
                {
                    if (kvp.Value is null) continue;
                    _crashlytics.SetCustomKey(kvp.Key, kvp.Value);
                }
            }
            _crashlytics.RecordException(exception.ToThrowable());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FirebaseCrashlyticsService.RecordNonFatal failed: {ex}");
        }
    }

    public void RecordFatal(Exception exception)
    {
        if (!_gate.CrashAllowed) return;
        try
        {
            _crashlytics.SetCustomKey("is_fatal", "true");
            _crashlytics.RecordException(exception.ToThrowable());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FirebaseCrashlyticsService.RecordFatal failed: {ex}");
        }
    }

    public void Log(string message)
    {
        if (!_gate.CrashAllowed) return;
        try
        {
            _crashlytics.Log(message);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FirebaseCrashlyticsService.Log failed: {ex}");
        }
    }

    public void SetUserId(string? userId)
    {
        try
        {
            _crashlytics.SetUserId(userId ?? string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FirebaseCrashlyticsService.SetUserId failed: {ex}");
        }
    }

    public void SetCustomKey(string key, string? value)
    {
        try
        {
            _crashlytics.SetCustomKey(key, value ?? string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FirebaseCrashlyticsService.SetCustomKey failed: {ex}");
        }
    }

    public void SetCrashlyticsCollectionEnabled(bool enabled)
    {
        try
        {
            _crashlytics.SetCrashlyticsCollectionEnabled(Java.Lang.Boolean.ValueOf(enabled));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FirebaseCrashlyticsService.SetCrashlyticsCollectionEnabled failed: {ex}");
        }
    }

    private void OnConsentChanged(object? sender, EventArgs e)
    {
        SetCrashlyticsCollectionEnabled(_gate.CrashAllowed);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gate.ConsentChanged -= OnConsentChanged;
    }
}
#endif
