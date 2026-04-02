using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using BookLoggerApp.Platforms.Android.Widgets.Services;

namespace BookLoggerApp.Platforms.Android.Widgets;

[BroadcastReceiver(Label = "Reading Streak", Exported = true)]
[IntentFilter(new[] { "android.appwidget.action.APPWIDGET_UPDATE" })]
[MetaData("android.appwidget.provider", Resource = "@xml/widget_reading_streak_info")]
public class ReadingStreakWidgetProvider : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context is null || appWidgetManager is null || appWidgetIds is null)
            return;

        foreach (var widgetId in appWidgetIds)
        {
            UpdateWidget(context, appWidgetManager, widgetId);
        }
    }

    private static void UpdateWidget(Context context, AppWidgetManager appWidgetManager, int widgetId)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.widget_reading_streak);

        try
        {
            var streakData = Task.Run(() => WidgetDataService.GetStreakDataAsync()).GetAwaiter().GetResult();

            views.SetTextViewText(Resource.Id.widget_streak_count, streakData.CurrentStreak.ToString());

            // Singular/plural
            views.SetTextViewText(Resource.Id.widget_streak_label,
                streakData.CurrentStreak == 1 ? "Day Streak" : "Day Streak");

            // Today status indicator
            if (streakData.ReadToday)
            {
                views.SetTextViewText(Resource.Id.widget_streak_today, "Read today \u2713");
            }
            else if (streakData.CurrentStreak > 0)
            {
                views.SetTextViewText(Resource.Id.widget_streak_today, "Not read today");
            }
            else
            {
                views.SetTextViewText(Resource.Id.widget_streak_today, "");
            }

            // Fire icon: dim when no streak
            views.SetTextViewText(Resource.Id.widget_streak_icon,
                streakData.CurrentStreak > 0 ? "\U0001F525" : "\U0001F4D6");
        }
        catch
        {
            views.SetTextViewText(Resource.Id.widget_streak_count, "—");
            views.SetTextViewText(Resource.Id.widget_streak_label, "Streak");
            views.SetTextViewText(Resource.Id.widget_streak_today, "");
        }

        // Click opens the app
        var intent = new Intent(context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        var pendingIntent = PendingIntent.GetActivity(
            context, 1, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        views.SetOnClickPendingIntent(Resource.Id.widget_streak_root, pendingIntent);

        appWidgetManager.UpdateAppWidget(widgetId, views);
    }
}
