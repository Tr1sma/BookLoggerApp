using Android.App;
using Android.Content;
using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace BookLoggerApp.Platforms.Android.Services;

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

        PersistState(timerState, sessionId, bookId, startUtc, elapsed, isRunning: false);
        timerState?.NotifyExternalCommand(ExternalTimerCommand.Pause);
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
