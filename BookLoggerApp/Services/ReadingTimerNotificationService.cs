using BookLoggerApp.Core.Resources;
using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Services;

/// <summary>
/// Drives the live reading-timer notification. On Android this is an ongoing
/// foreground-service notification (lock screen + status bar). No-op elsewhere.
///
/// Showing is gated by the user's settings (master notifications + the dedicated
/// live-timer toggle). The effective flag is cached and refreshed on
/// <see cref="IAppSettingsProvider.SettingsChanged"/>, because <see cref="ShowRunning"/>
/// is called from synchronous timer-transition code paths.
///
/// Display strings are resolved here via <see cref="IStringLocalizer{T}"/> so the
/// notification matches the in-app language (not just the device locale) and passed
/// down to the native layer.
/// </summary>
public class ReadingTimerNotificationService : IReadingTimerNotificationService, IDisposable
{
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly IStringLocalizer<AppResources> _localizer;
    private volatile bool _enabled = true;

    public ReadingTimerNotificationService(
        IAppSettingsProvider settingsProvider,
        IStringLocalizer<AppResources> localizer)
    {
        _settingsProvider = settingsProvider;
        _localizer = localizer;
        _settingsProvider.SettingsChanged += OnSettingsChanged;
        _ = RefreshEnabledAsync();
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => _ = RefreshEnabledAsync();

    private async Task RefreshEnabledAsync()
    {
        try
        {
            var settings = await _settingsProvider.GetSettingsAsync();
            _enabled = settings.NotificationsEnabled && settings.LiveTimerNotificationEnabled;
        }
        catch
        {
            // Settings unavailable (e.g. DB still initializing) — leave the last known
            // value; SettingsChanged will refresh once the user touches settings.
        }
    }

    private ReadingTimerNotificationLabels BuildLabels() => new(
        Reading: _localizer["Notification_Timer_Reading"],
        Paused: _localizer["Notification_Timer_Paused"],
        Pause: _localizer["Notification_Timer_Pause"],
        Resume: _localizer["Notification_Timer_Resume"],
        Stop: _localizer["Notification_Timer_Stop"]);

    public void ShowRunning(ReadingTimerNotificationData data)
    {
        if (!_enabled) return;
#if ANDROID
        try
        {
            Platforms.Android.Services.ReadingTimerForegroundService.Start(
                global::Android.App.Application.Context, data, isRunning: true, BuildLabels());
        }
        catch
        {
            // The notification is best-effort — never crash the timer.
        }
#endif
    }

    public void ShowPaused(ReadingTimerNotificationData data)
    {
        if (!_enabled) return;
#if ANDROID
        try
        {
            Platforms.Android.Services.ReadingTimerForegroundService.Start(
                global::Android.App.Application.Context, data, isRunning: false, BuildLabels());
        }
        catch
        {
            // Best-effort.
        }
#endif
    }

    public void Hide()
    {
        // Always allowed — used both on session end and when the user disables the feature.
#if ANDROID
        try
        {
            Platforms.Android.Services.ReadingTimerForegroundService.Stop(
                global::Android.App.Application.Context);
        }
        catch
        {
            // Best-effort.
        }
#endif
    }

    public void Dispose()
    {
        _settingsProvider.SettingsChanged -= OnSettingsChanged;
    }
}
