using Android.Appwidget;
using Android.Content;
using Android.OS;
using Android.Widget;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace BookLoggerApp.Platforms.Android.Widgets;

[BroadcastReceiver(Name = "com.bookheart.app.BookProgressWidgetProvider", Exported = true)]
[IntentFilter([AppWidgetManager.ActionAppwidgetUpdate])]
[MetaData("android.appwidget.provider", Resource = "@xml/book_progress_widget_info")]
public class BookProgressWidgetProvider : AppWidgetProvider
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        base.OnReceive(context, intent);

        if (context == null || intent?.Action != WidgetConstants.ActionUpdateWidget)
        {
            return;
        }

        var appWidgetManager = AppWidgetManager.GetInstance(context);
        var thisWidget = new ComponentName(context, Java.Lang.Class.FromType(typeof(BookProgressWidgetProvider)));
        var widgetIds = appWidgetManager.GetAppWidgetIds(thisWidget);
        if (widgetIds.Length == 0)
        {
            return;
        }

        OnUpdate(context, appWidgetManager, widgetIds);
    }

    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        base.OnUpdate(context, appWidgetManager, appWidgetIds);

        if (context == null || appWidgetManager == null || appWidgetIds == null || appWidgetIds.Length == 0)
        {
            return;
        }

        var services = MauiApplication.Current?.Services;
        if (services == null)
        {
            return;
        }

        foreach (var appWidgetId in appWidgetIds)
        {
            _ = UpdateWidgetAsync(context, appWidgetManager, appWidgetId, services);
        }
    }

    public override void OnAppWidgetOptionsChanged(
        Context? context,
        AppWidgetManager? appWidgetManager,
        int appWidgetId,
        Bundle? newOptions)
    {
        base.OnAppWidgetOptionsChanged(context, appWidgetManager, appWidgetId, newOptions);
        OnUpdate(context, appWidgetManager, [appWidgetId]);
    }

    private static void ApplyData(RemoteViews views, WidgetData data, string contentMode)
    {
        views.SetTextViewText(Resource.Id.current_book_title, data.CurrentBookTitle ?? "Kein aktives Buch");
        views.SetTextViewText(Resource.Id.current_book_progress_text, data.CurrentBookProgressText);
        views.SetProgressBar(Resource.Id.current_book_progress_bar, 100, data.CurrentBookProgressPercent, false);

        views.SetTextViewText(Resource.Id.reading_streak_text, $"{data.StreakDays} Tage");
        views.SetTextViewText(Resource.Id.daily_goal_title, data.DailyGoalTitle ?? "Tagesziel");
        views.SetTextViewText(Resource.Id.daily_goal_progress_text, data.DailyGoalProgressText);
        views.SetProgressBar(Resource.Id.daily_goal_progress_bar, 100, data.DailyGoalProgressPercent, false);

        SetSectionVisibility(views, Resource.Id.current_book_section, contentMode == WidgetConstants.ContentModeBook || contentMode == WidgetConstants.ContentModeAll);
        SetSectionVisibility(views, Resource.Id.streak_section, contentMode == WidgetConstants.ContentModeStreak || contentMode == WidgetConstants.ContentModeAll);
        SetSectionVisibility(views, Resource.Id.daily_goal_section, contentMode == WidgetConstants.ContentModeDailyGoal || contentMode == WidgetConstants.ContentModeAll);
    }

    private static void SetSectionVisibility(RemoteViews views, int viewId, bool visible)
    {
        views.SetViewVisibility(viewId, visible ? Android.Views.ViewStates.Visible : Android.Views.ViewStates.Gone);
    }

    private static PendingIntent CreateRefreshPendingIntent(Context context)
    {
        var intent = new Intent(context, typeof(BookProgressWidgetProvider));
        intent.SetAction(WidgetConstants.ActionUpdateWidget);

        var flags = PendingIntentFlags.UpdateCurrent;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            flags |= PendingIntentFlags.Immutable;
        }

        return PendingIntent.GetBroadcast(context, 0, intent, flags);
    }

    private static async Task UpdateWidgetAsync(
        Context context,
        AppWidgetManager appWidgetManager,
        int appWidgetId,
        IServiceProvider services)
    {
        var dataService = services.GetService<IWidgetDataService>();

        WidgetData widgetData;
        try
        {
            widgetData = dataService == null
                ? CreateFallbackData()
                : await dataService.GetWidgetDataAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("BookProgressWidget", $"Widget update failed: {ex}");
            widgetData = CreateFallbackData();
        }

        var options = appWidgetManager.GetAppWidgetOptions(appWidgetId);
        var useLargeLayout = (options?.GetInt(AppWidgetManager.OptionAppwidgetMinHeight) ?? 0) >= 220;
        var layout = useLargeLayout
            ? Resource.Layout.book_progress_widget_large
            : Resource.Layout.book_progress_widget_compact;

        var views = new RemoteViews(context.PackageName, layout);
        var contentMode = GetContentMode(context, appWidgetId, options);
        ApplyData(views, widgetData, contentMode);
        views.SetOnClickPendingIntent(Resource.Id.widget_root, CreateRefreshPendingIntent(context));
        appWidgetManager.UpdateAppWidget(appWidgetId, views);
    }

    private static string GetContentMode(Context context, int appWidgetId, Bundle? options)
    {
        var optionMode = options?.GetString(WidgetConstants.ExtraContentMode);
        if (!string.IsNullOrWhiteSpace(optionMode))
        {
            return optionMode;
        }

        var prefs = context.GetSharedPreferences(WidgetConstants.PreferencesName, FileCreationMode.Private);
        return prefs?.GetString($"{WidgetConstants.ExtraContentMode}_{appWidgetId}", WidgetConstants.ContentModeAll)
            ?? WidgetConstants.ContentModeAll;
    }

    private static WidgetData CreateFallbackData()
    {
        return new WidgetData
        {
            CurrentBookTitle = "Widget",
            CurrentBookProgressText = "Daten werden aktualisiert",
            DailyGoalTitle = "Tagesziel",
            DailyGoalProgressText = "Daten werden aktualisiert"
        };
    }
}
