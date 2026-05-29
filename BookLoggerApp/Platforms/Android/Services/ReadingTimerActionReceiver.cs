using Android.App;
using Android.Content;
using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace BookLoggerApp.Platforms.Android.Services;

/// <summary>
/// Handles the reading-timer notification's background action buttons (Pause / Resume).
/// It relays the command to the live in-app timer (via <see cref="ITimerStateService"/>) and
/// rebuilds the notification from the intent extras, so it stays correct even when no timer
/// component is currently mounted. The Stop button is NOT handled here — it opens the app
/// directly (a background receiver can't launch an activity on Android 10+); see
/// <c>ReadingTimerForegroundService.BuildStopAction</c> and <c>MainActivity</c>.
/// </summary>
[BroadcastReceiver(Exported = false)]
public class ReadingTimerActionReceiver : BroadcastReceiver
{
    public const string ActionPause = "com.bookheart.app.timer.PAUSE";
    public const string ActionResume = "com.bookheart.app.timer.RESUME";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent?.Action is null)
            return;

        var sessionIdStr = intent.GetStringExtra(ReadingTimerForegroundService.ExtraSessionId);
        var bookIdStr = intent.GetStringExtra(ReadingTimerForegroundService.ExtraBookId);
        var title = intent.GetStringExtra(ReadingTimerForegroundService.ExtraTitle) ?? "BookHeart";
        long startUnixMs = intent.GetLongExtra(ReadingTimerForegroundService.ExtraStartUnixMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        long elapsedMs = intent.GetLongExtra(ReadingTimerForegroundService.ExtraElapsedMs, 0L);

        Guid.TryParse(sessionIdStr, out var sessionId);
        Guid.TryParse(bookIdStr, out var bookId);

        // The in-app-localized labels were carried forward on the action intent so the
        // rebuilt notification keeps the user's language even with no component mounted.
        var labels = new ReadingTimerNotificationLabels(
            Reading: intent.GetStringExtra(ReadingTimerForegroundService.ExtraLabelReading) ?? "Reading",
            Paused: intent.GetStringExtra(ReadingTimerForegroundService.ExtraLabelPaused) ?? "Paused",
            Pause: intent.GetStringExtra(ReadingTimerForegroundService.ExtraLabelPause) ?? "Pause",
            Resume: intent.GetStringExtra(ReadingTimerForegroundService.ExtraLabelResume) ?? "Resume",
            Stop: intent.GetStringExtra(ReadingTimerForegroundService.ExtraLabelStop) ?? "Stop");

        var timerState = global::Microsoft.Maui.IPlatformApplication.Current?.Services?.GetService<ITimerStateService>();

        switch (intent.Action)
        {
            case ActionPause:
                Pause(context, timerState, sessionId, bookId, title, startUnixMs, labels);
                break;
            case ActionResume:
                Resume(context, timerState, sessionId, bookId, title, elapsedMs, labels);
                break;
        }
    }

    private static void Pause(Context context, ITimerStateService? timerState, Guid sessionId, Guid bookId, string title, long startUnixMs, ReadingTimerNotificationLabels labels)
    {
        var startUtc = DateTimeOffset.FromUnixTimeMilliseconds(startUnixMs).UtcDateTime;
        var elapsed = DateTime.UtcNow - startUtc;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;

        // Persist the paused state so the inline timer restores as paused when the book page
        // (re)mounts — critical when no component is currently subscribed (app backgrounded or
        // process killed), where NotifyExternalCommand below is a no-op.
        PersistState(timerState, sessionId, bookId, startUtc, elapsed, isRunning: false);

        // Relay to the in-app timer (no-op if no component is mounted).
        timerState?.NotifyExternalCommand(ExternalTimerCommand.Pause);

        // Rebuild the notification in paused form (fallback when nothing is mounted).
        var data = new ReadingTimerNotificationData(sessionId, bookId, title, startUtc, elapsed, IsRunning: false);
        ReadingTimerForegroundService.Start(context, data, isRunning: false, labels);
    }

    private static void Resume(Context context, ITimerStateService? timerState, Guid sessionId, Guid bookId, string title, long elapsedMs, ReadingTimerNotificationLabels labels)
    {
        var elapsed = TimeSpan.FromMilliseconds(elapsedMs < 0 ? 0 : elapsedMs);
        var startUtc = DateTime.UtcNow - elapsed;

        PersistState(timerState, sessionId, bookId, startUtc, elapsed, isRunning: true);

        timerState?.NotifyExternalCommand(ExternalTimerCommand.Resume);

        var data = new ReadingTimerNotificationData(sessionId, bookId, title, startUtc, elapsed, IsRunning: true);
        ReadingTimerForegroundService.Start(context, data, isRunning: true, labels);
    }

    /// <summary>
    /// Mirrors the running/paused state into the persisted timer state (MAUI Preferences) so
    /// <c>ReadingTimerInline.TryRestoreTimerState</c> shows the correct state on the next mount,
    /// matching what a mounted component's Pause/Resume would have written.
    /// </summary>
    private static void PersistState(ITimerStateService? timerState, Guid sessionId, Guid bookId, DateTime startUtc, TimeSpan elapsed, bool isRunning)
    {
        if (timerState is null || sessionId == Guid.Empty || bookId == Guid.Empty)
            return;

        timerState.SaveState(new TimerStateData
        {
            SessionId = sessionId,
            BookId = bookId,
            StartTimeTicks = startUtc.Ticks,
            IsRunning = isRunning,
            PausedElapsedTicks = isRunning ? 0 : elapsed.Ticks
        });
    }
}
