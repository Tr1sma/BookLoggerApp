using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Platforms.Android.Services;

[Service(
    Name = "com.bookheart.app.ReadingTimerForegroundService",
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeSpecialUse)]
public class ReadingTimerForegroundService : Service
{
    public const int NotificationId = 7100;
    public const string ChannelId = "bookheart_timer";

    public const string ExtraIsRunning = "extra_is_running";
    public const string ExtraSessionId = "extra_session_id";
    public const string ExtraBookId = "extra_book_id";
    public const string ExtraTitle = "extra_title";
    public const string ExtraStartUnixMs = "extra_start_unix_ms";
    public const string ExtraElapsedMs = "extra_elapsed_ms";
    public const string ExtraLabelReading = "extra_label_reading";
    public const string ExtraLabelPaused = "extra_label_paused";
    public const string ExtraLabelPause = "extra_label_pause";
    public const string ExtraLabelResume = "extra_label_resume";
    public const string ExtraLabelStop = "extra_label_stop";

    // Stop button: pause session, then navigate to book page to confirm save
    public const string ExtraStopRequested = "extra_stop_requested";

    public override IBinder? OnBind(Intent? intent) => null;

    public static void Start(Context context, ReadingTimerNotificationData data, bool isRunning, ReadingTimerNotificationLabels labels)
    {
        var intent = new Intent(context, typeof(ReadingTimerForegroundService));
        intent.PutExtra(ExtraIsRunning, isRunning);
        intent.PutExtra(ExtraSessionId, data.SessionId.ToString());
        intent.PutExtra(ExtraBookId, data.BookId.ToString());
        intent.PutExtra(ExtraTitle, data.BookTitle);
        intent.PutExtra(ExtraStartUnixMs, ToUnixMs(data.StartTimeUtc));
        intent.PutExtra(ExtraElapsedMs, (long)data.Elapsed.TotalMilliseconds);
        intent.PutExtra(ExtraLabelReading, labels.Reading);
        intent.PutExtra(ExtraLabelPaused, labels.Paused);
        intent.PutExtra(ExtraLabelPause, labels.Pause);
        intent.PutExtra(ExtraLabelResume, labels.Resume);
        intent.PutExtra(ExtraLabelStop, labels.Stop);

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            context.StartForegroundService(intent);
        else
            context.StartService(intent);
    }

    public static void Stop(Context context)
        => context.StopService(new Intent(context, typeof(ReadingTimerForegroundService)));

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent is null)
        {
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        var notification = BuildNotification(intent);

        // specialUse type requires API 34+
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            ServiceCompat.StartForeground(
                this, NotificationId, notification, (int)ForegroundService.TypeSpecialUse);
        }
        else
        {
            StartForeground(NotificationId, notification);
        }

        return StartCommandResult.RedeliverIntent;
    }

    public override void OnDestroy()
    {
        ServiceCompat.StopForeground(this, ServiceCompat.StopForegroundRemove);
        base.OnDestroy();
    }

    private Notification BuildNotification(Intent intent)
    {
        bool isRunning = intent.GetBooleanExtra(ExtraIsRunning, true);
        string title = intent.GetStringExtra(ExtraTitle) ?? "BookHeart";
        string bookId = intent.GetStringExtra(ExtraBookId) ?? string.Empty;
        long startUnixMs = intent.GetLongExtra(ExtraStartUnixMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        long elapsedMs = intent.GetLongExtra(ExtraElapsedMs, 0L);

        string labelPause = intent.GetStringExtra(ExtraLabelPause) ?? "Pause";
        string labelResume = intent.GetStringExtra(ExtraLabelResume) ?? "Resume";
        string labelStop = intent.GetStringExtra(ExtraLabelStop) ?? "Stop";
        string status = (isRunning
            ? intent.GetStringExtra(ExtraLabelReading)
            : intent.GetStringExtra(ExtraLabelPaused)) ?? (isRunning ? "Reading" : "Paused");

        var builder = new NotificationCompat.Builder(this, ChannelId);
        builder.SetSmallIcon(Resource.Drawable.ic_launcher_monochrome);
        builder.SetContentTitle(title);
        builder.SetOngoing(true);
        builder.SetOnlyAlertOnce(true);
        builder.SetVisibility((int)NotificationCompat.VisibilityPublic);
        builder.SetCategory(NotificationCompat.CategoryStopwatch);
        builder.SetContentIntent(BuildContentIntent(bookId));

        if (isRunning)
        {
            builder.SetUsesChronometer(true);
            builder.SetWhen(startUnixMs);
            builder.SetShowWhen(true);
            builder.SetContentText(status);
            builder.AddAction(BuildAction(
                global::Android.Resource.Drawable.IcMediaPause,
                labelPause,
                ReadingTimerActionReceiver.ActionPause, requestCode: 1, intent));
        }
        else
        {
            builder.SetUsesChronometer(false);
            builder.SetShowWhen(false);
            builder.SetContentText($"{status} · {FormatElapsed(elapsedMs)}");
            builder.AddAction(BuildAction(
                global::Android.Resource.Drawable.IcMediaPlay,
                labelResume,
                ReadingTimerActionReceiver.ActionResume, requestCode: 2, intent));
        }

        // Stop uses GetActivity; receiver can't launch activity on Android 10+
        builder.AddAction(BuildStopAction(
            global::Android.Resource.Drawable.IcMenuCloseClearCancel,
            labelStop,
            bookId));

        return builder.Build()!;
    }

    private NotificationCompat.Action BuildStopAction(int icon, string title, string bookId)
    {
        var launch = new Intent(this, typeof(MainActivity));
        launch.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        if (!string.IsNullOrEmpty(bookId))
            launch.PutExtra(ExtraBookId, bookId);
        launch.PutExtra(ExtraStopRequested, true);

        var pendingIntent = PendingIntent.GetActivity(
            this, 4, launch,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        return new NotificationCompat.Action(icon, title, pendingIntent);
    }

    private NotificationCompat.Action BuildAction(int icon, string title, string action, int requestCode, Intent source)
    {
        var actionIntent = new Intent(this, typeof(ReadingTimerActionReceiver));
        actionIntent.SetAction(action);
        actionIntent.PutExtra(ExtraSessionId, source.GetStringExtra(ExtraSessionId));
        actionIntent.PutExtra(ExtraBookId, source.GetStringExtra(ExtraBookId));
        actionIntent.PutExtra(ExtraTitle, source.GetStringExtra(ExtraTitle));
        actionIntent.PutExtra(ExtraStartUnixMs, source.GetLongExtra(ExtraStartUnixMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        actionIntent.PutExtra(ExtraElapsedMs, source.GetLongExtra(ExtraElapsedMs, 0L));
        actionIntent.PutExtra(ExtraLabelReading, source.GetStringExtra(ExtraLabelReading));
        actionIntent.PutExtra(ExtraLabelPaused, source.GetStringExtra(ExtraLabelPaused));
        actionIntent.PutExtra(ExtraLabelPause, source.GetStringExtra(ExtraLabelPause));
        actionIntent.PutExtra(ExtraLabelResume, source.GetStringExtra(ExtraLabelResume));
        actionIntent.PutExtra(ExtraLabelStop, source.GetStringExtra(ExtraLabelStop));

        var pendingIntent = PendingIntent.GetBroadcast(
            this, requestCode, actionIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        return new NotificationCompat.Action(icon, title, pendingIntent);
    }

    private PendingIntent? BuildContentIntent(string bookId)
    {
        var launch = new Intent(this, typeof(MainActivity));
        launch.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        if (!string.IsNullOrEmpty(bookId))
            launch.PutExtra(ExtraBookId, bookId);

        return PendingIntent.GetActivity(
            this, 0, launch,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
    }

    private static long ToUnixMs(DateTime utc)
        => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static string FormatElapsed(long ms)
    {
        var t = TimeSpan.FromMilliseconds(ms < 0 ? 0 : ms);
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes:D2}:{t.Seconds:D2}";
    }
}
