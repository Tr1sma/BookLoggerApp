using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.OS;
using Android.Widget;

namespace BookLoggerApp.Platforms.Android.Widgets;

[Activity(Name = "com.bookheart.app.BookProgressWidgetConfigureActivity", Exported = false)]
public class BookProgressWidgetConfigureActivity : Activity
{
    private int _appWidgetId = AppWidgetManager.InvalidAppwidgetId;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        SetResult(Result.Canceled);

        _appWidgetId = Intent?.Extras?.GetInt(
            AppWidgetManager.ExtraAppwidgetId,
            AppWidgetManager.InvalidAppwidgetId) ?? AppWidgetManager.InvalidAppwidgetId;

        if (_appWidgetId == AppWidgetManager.InvalidAppwidgetId)
        {
            Finish();
            return;
        }

        SetContentView(Resource.Layout.book_progress_widget_configure);

        WireButton(Resource.Id.widget_config_show_all, WidgetConstants.ContentModeAll);
        WireButton(Resource.Id.widget_config_show_book, WidgetConstants.ContentModeBook);
        WireButton(Resource.Id.widget_config_show_streak, WidgetConstants.ContentModeStreak);
        WireButton(Resource.Id.widget_config_show_goal, WidgetConstants.ContentModeDailyGoal);
    }

    private void WireButton(int buttonId, string mode)
    {
        if (FindViewById<Button>(buttonId) is not { } button)
        {
            return;
        }

        button.Click += (_, _) => CompleteConfiguration(mode);
    }

    private void CompleteConfiguration(string contentMode)
    {
        var prefs = GetSharedPreferences(WidgetConstants.PreferencesName, FileCreationMode.Private);
        var editor = prefs?.Edit();
        editor?.PutString($"{WidgetConstants.ExtraContentMode}_{_appWidgetId}", contentMode);
        editor?.Apply();

        var appWidgetManager = AppWidgetManager.GetInstance(this);
        var provider = new BookProgressWidgetProvider();
        provider.OnUpdate(this, appWidgetManager, [_appWidgetId]);

        var resultValue = new Intent();
        resultValue.PutExtra(AppWidgetManager.ExtraAppwidgetId, _appWidgetId);
        SetResult(Result.Ok, resultValue);
        Finish();
    }
}
