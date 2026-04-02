using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using BookLoggerApp.Platforms.Android.Widgets.Models;
using BookLoggerApp.Platforms.Android.Widgets.Services;

namespace BookLoggerApp.Platforms.Android.Widgets;

[BroadcastReceiver(Label = "Lese-Ziel", Exported = true)]
[IntentFilter(new[] { "android.appwidget.action.APPWIDGET_UPDATE" })]
[MetaData("android.appwidget.provider", Resource = "@xml/widget_daily_goal_info")]
public class DailyGoalWidgetProvider : AppWidgetProvider
{
    internal const string PrefsName = "bookheart_widget_prefs";
    internal const string PrefKeyGoalIdPrefix = "widget_goal_id_";

    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context is null || appWidgetManager is null || appWidgetIds is null)
            return;

        foreach (var widgetId in appWidgetIds)
        {
            UpdateWidget(context, appWidgetManager, widgetId);
        }
    }

    public override void OnDeleted(Context? context, int[]? appWidgetIds)
    {
        // Clean up SharedPreferences when widget is removed
        if (context is null || appWidgetIds is null)
            return;

        var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
        var editor = prefs?.Edit();
        if (editor is null) return;

        foreach (var id in appWidgetIds)
        {
            editor.Remove($"{PrefKeyGoalIdPrefix}{id}");
        }
        editor.Apply();

        base.OnDeleted(context, appWidgetIds);
    }

    internal static void UpdateWidget(Context context, AppWidgetManager appWidgetManager, int widgetId)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.widget_daily_goal);

        try
        {
            // Read configured goal ID from SharedPreferences
            var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            var goalIdStr = prefs?.GetString($"{PrefKeyGoalIdPrefix}{widgetId}", null);
            Guid? goalId = Guid.TryParse(goalIdStr, out var parsed) ? parsed : null;

            var goalData = Task.Run(() => WidgetDataService.GetGoalDataByIdAsync(goalId)).GetAwaiter().GetResult();

            if (goalData is not null)
            {
                views.SetTextViewText(Resource.Id.widget_goal_title, goalData.Title);
                views.SetProgressBar(Resource.Id.widget_goal_progress_bar, 100,
                    Math.Min(goalData.ProgressPercentage, 100), false);
                views.SetTextViewText(Resource.Id.widget_goal_percent, $"{goalData.ProgressPercentage}%");

                // Goal type icon and detail text
                var (icon, unit) = goalData.GoalType switch
                {
                    "Books" => ("\U0001F4DA", "Buecher"),
                    "Pages" => ("\U0001F4C4", "Seiten"),
                    "Minutes" => ("\u23F1\uFE0F", "Minuten"),
                    _ => ("\U0001F3AF", "")
                };
                views.SetTextViewText(Resource.Id.widget_goal_icon, icon);
                views.SetTextViewText(Resource.Id.widget_goal_detail,
                    $"{goalData.Current}/{goalData.Target} {unit}");
            }
            else
            {
                views.SetTextViewText(Resource.Id.widget_goal_title, "Kein aktives Ziel");
                views.SetTextViewText(Resource.Id.widget_goal_icon, "\U0001F3AF");
                views.SetProgressBar(Resource.Id.widget_goal_progress_bar, 100, 0, false);
                views.SetTextViewText(Resource.Id.widget_goal_percent, "");
                views.SetTextViewText(Resource.Id.widget_goal_detail, "Erstelle ein Ziel in BookHeart");
            }
        }
        catch
        {
            views.SetTextViewText(Resource.Id.widget_goal_title, "BookHeart");
            views.SetTextViewText(Resource.Id.widget_goal_detail, "Tippe um zu oeffnen");
        }

        // Click opens the app
        var intent = new Intent(context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        var pendingIntent = PendingIntent.GetActivity(
            context, 2, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        views.SetOnClickPendingIntent(Resource.Id.widget_goal_root, pendingIntent);

        appWidgetManager.UpdateAppWidget(widgetId, views);
    }
}
