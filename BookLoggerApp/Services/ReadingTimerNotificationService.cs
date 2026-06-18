using BookLoggerApp.Core.Resources;
using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Services;

/// <summary>
/// Drives the live reading-timer foreground notification (Android lock screen/status bar).
/// Enabled flag is cached and refreshed via SettingsChanged because ShowRunning is called
/// from synchronous timer-transition paths. Strings use in-app locale, not device locale.
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
            // DB still initializing — keep last known value; SettingsChanged will retry.
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
            // Best-effort — never crash the timer.
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
