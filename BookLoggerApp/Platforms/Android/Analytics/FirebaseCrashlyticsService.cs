#if ANDROID
using BookLoggerApp.Core.Services.Abstractions;
using Firebase.Crashlytics;

namespace BookLoggerApp.Platforms.AndroidImpl.Analytics;

public sealed class FirebaseCrashlyticsService : ICrashReportingService, IDisposable
{
    private readonly IAnalyticsConsentGate _gate;
    private FirebaseCrashlytics? _crashlytics;
    private bool _initFailed;
    private bool _disposed;

    public FirebaseCrashlyticsService(IAnalyticsConsentGate gate)
    {
        // Do NOT resolve FirebaseCrashlytics.Instance here — FirebaseApp may not be
        // initialized yet when DI constructs this service (runs during MainApplication.OnCreate,
        // which precedes MainActivity.OnCreate where explicit FirebaseApp.InitializeApp happens).
        // We resolve lazily on first use; see GetCrashlytics().
        _gate = gate;
        _gate.ConsentChanged += OnConsentChanged;
    }

    private FirebaseCrashlytics? GetCrashlytics()
    {
        if (_crashlytics is not null) return _crashlytics;
        if (_initFailed) return null;

        try
        {
            _crashlytics = FirebaseCrashlytics.Instance;
            return _crashlytics;
        }
        catch (Exception firstEx)
        {
            System.Diagnostics.Debug.WriteLine($"FirebaseCrashlytics.Instance failed, trying explicit InitializeApp: {firstEx}");
            try
            {
                Firebase.FirebaseApp.InitializeApp(global::Android.App.Application.Context);
                _crashlytics = FirebaseCrashlytics.Instance;
                return _crashlytics;
            }
            catch (Exception fallbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"FirebaseCrashlytics fallback init failed: {fallbackEx}");
                _initFailed = true;
                return null;
            }
        }
    }

    public void RecordNonFatal(Exception exception, IReadOnlyDictionary<string, string>? customKeys = null)
    {
        if (!_gate.CrashAllowed) return;
        try
        {
            var crash = GetCrashlytics();
            if (crash is null) return;

            if (customKeys is not null)
            {
                foreach (var kvp in customKeys)
                {
                    if (kvp.Value is null) continue;
                    crash.SetCustomKey(kvp.Key, kvp.Value);
                }
            }
            crash.RecordException(exception.ToThrowable());
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
            var crash = GetCrashlytics();
            if (crash is null) return;
            crash.SetCustomKey("is_fatal", "true");
            crash.RecordException(exception.ToThrowable());
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
            GetCrashlytics()?.Log(message);
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
            GetCrashlytics()?.SetUserId(userId ?? string.Empty);
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
            GetCrashlytics()?.SetCustomKey(key, value ?? string.Empty);
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
            GetCrashlytics()?.SetCrashlyticsCollectionEnabled(Java.Lang.Boolean.ValueOf(enabled));
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
