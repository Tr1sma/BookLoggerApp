using Android.Appwidget;
using Android.Content;

namespace BookLoggerApp.Platforms.Android.Widgets;

/// <summary>
/// Static helper to broadcast widget update intents to all BookHeart widget providers.
/// Called from within the running app (where MAUI DI is available) after data changes.
/// </summary>
public static class WidgetUpdateHelper
{
    public static void UpdateAllWidgets(Context context)
    {
        UpdateWidget<CurrentBookWidgetProvider>(context);
        UpdateWidget<ReadingStreakWidgetProvider>(context);
        UpdateWidget<DailyGoalWidgetProvider>(context);
    }

    public static void UpdateWidget<TProvider>(Context context) where TProvider : AppWidgetProvider
    {
        var appWidgetManager = AppWidgetManager.GetInstance(context);
        if (appWidgetManager is null) return;

        var componentName = new ComponentName(context, Java.Lang.Class.FromType(typeof(TProvider)));
        var ids = appWidgetManager.GetAppWidgetIds(componentName);

        if (ids is null || ids.Length == 0) return;

        var intent = new Intent(context, typeof(TProvider));
        intent.SetAction(AppWidgetManager.ActionAppwidgetUpdate);
        intent.PutExtra(AppWidgetManager.ExtraAppwidgetIds, ids);
        context.SendBroadcast(intent);
    }
}
