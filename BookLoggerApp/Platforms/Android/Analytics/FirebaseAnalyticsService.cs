#if ANDROID
using BookLoggerApp.Core.Services.Abstractions;
using Firebase.Analytics;
using Microsoft.Maui.ApplicationModel;

namespace BookLoggerApp.Platforms.AndroidImpl.Analytics;

public sealed class FirebaseAnalyticsService : IAnalyticsService, IDisposable
{
    private readonly IAnalyticsConsentGate _gate;
    private FirebaseAnalytics? _analytics;
    private bool _initFailed;
    private bool _disposed;

    public FirebaseAnalyticsService(IAnalyticsConsentGate gate)
    {
        // Do NOT resolve FirebaseAnalytics.GetInstance here — FirebaseApp may not be
        // initialized yet when DI constructs this service. See GetAnalytics() below.
        _gate = gate;
        _gate.ConsentChanged += OnConsentChanged;
    }

    private FirebaseAnalytics? GetAnalytics()
    {
        if (_analytics is not null) return _analytics;
        if (_initFailed) return null;

        try
        {
            var ctx = global::Android.App.Application.Context;
            _analytics = FirebaseAnalytics.GetInstance(ctx);
            return _analytics;
        }
        catch (Exception firstEx)
        {
            System.Diagnostics.Debug.WriteLine($"FirebaseAnalytics.GetInstance failed, trying explicit InitializeApp: {firstEx}");
            try
            {
                var ctx = global::Android.App.Application.Context;
                Firebase.FirebaseApp.InitializeApp(ctx);
                _analytics = FirebaseAnalytics.GetInstance(ctx);
                return _analytics;
            }
            catch (Exception fallbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"FirebaseAnalytics fallback init failed: {fallbackEx}");
                _initFailed = true;
                return null;
            }
        }
    }

    public void LogEvent(string name, IDictionary<string, object?>? parameters = null)
    {
        if (!_gate.AnalyticsAllowed) return;
        try
        {
            GetAnalytics()?.LogEvent(name, parameters.ToBundle());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FirebaseAnalyticsService.LogEvent failed: {ex}");
        }
    }

    public void LogScreenView(string screenName, string? screenClass = null)
    {
        if (!_gate.AnalyticsAllowed) return;
        try
        {
            var analytics = GetAnalytics();
            if (analytics is null) return;

            var bundle = new global::Android.OS.Bundle();
            bundle.PutString(FirebaseAnalytics.Param.ScreenName, screenName);
            if (!string.IsNullOrEmpty(screenClass))
            {
                bundle.PutString(FirebaseAnalytics.Param.ScreenClass, screenClass);
            }
            analytics.LogEvent(FirebaseAnalytics.Event.ScreenView, bundle);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FirebaseAnalyticsService.LogScreenView failed: {ex}");
        }
    }

    public void SetUserProperty(string name, string? value)
    {
        try
        {
            GetAnalytics()?.SetUserProperty(name, value);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FirebaseAnalyticsService.SetUserProperty failed: {ex}");
        }
    }

    public void SetUserId(string? userId)
    {
        try
        {
            GetAnalytics()?.SetUserId(userId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FirebaseAnalyticsService.SetUserId failed: {ex}");
        }
    }

    public void SetAnalyticsCollectionEnabled(bool enabled)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    GetAnalytics()?.SetAnalyticsCollectionEnabled(enabled);
                }
                catch (Exception innerEx)
                {
                    System.Diagnostics.Debug.WriteLine($"SetAnalyticsCollectionEnabled inner failed: {innerEx}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FirebaseAnalyticsService.SetAnalyticsCollectionEnabled failed: {ex}");
        }
    }

    public void ResetAnalyticsData()
    {
        try
        {
            GetAnalytics()?.ResetAnalyticsData();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FirebaseAnalyticsService.ResetAnalyticsData failed: {ex}");
        }
    }

    private void OnConsentChanged(object? sender, EventArgs e)
    {
        var allowed = _gate.AnalyticsAllowed;
        SetAnalyticsCollectionEnabled(allowed);
        if (!allowed)
        {
            ResetAnalyticsData();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gate.ConsentChanged -= OnConsentChanged;
    }
}
#endif
