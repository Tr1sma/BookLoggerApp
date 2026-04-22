#if ANDROID
using BookLoggerApp.Core.Services.Abstractions;
using Firebase.Analytics;
using Microsoft.Maui.ApplicationModel;

namespace BookLoggerApp.Platforms.AndroidImpl.Analytics;

public sealed class FirebaseAnalyticsService : IAnalyticsService, IDisposable
{
    private readonly IAnalyticsConsentGate _gate;
    private readonly FirebaseAnalytics _analytics;
    private bool _disposed;

    public FirebaseAnalyticsService(IAnalyticsConsentGate gate)
    {
        _gate = gate;
        var ctx = global::Android.App.Application.Context;
        _analytics = FirebaseAnalytics.GetInstance(ctx);
        _gate.ConsentChanged += OnConsentChanged;
    }

    public void LogEvent(string name, IDictionary<string, object?>? parameters = null)
    {
        if (!_gate.AnalyticsAllowed) return;
        try
        {
            _analytics.LogEvent(name, parameters.ToBundle());
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
            var bundle = new global::Android.OS.Bundle();
            bundle.PutString(FirebaseAnalytics.Param.ScreenName, screenName);
            if (!string.IsNullOrEmpty(screenClass))
            {
                bundle.PutString(FirebaseAnalytics.Param.ScreenClass, screenClass);
            }
            _analytics.LogEvent(FirebaseAnalytics.Event.ScreenView, bundle);
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
            _analytics.SetUserProperty(name, value);
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
            _analytics.SetUserId(userId);
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
                    _analytics.SetAnalyticsCollectionEnabled(enabled);
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
            _analytics.ResetAnalyticsData();
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
