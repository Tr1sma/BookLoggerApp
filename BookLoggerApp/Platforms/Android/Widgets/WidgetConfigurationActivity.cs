using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using BookLoggerApp.Platforms.Android.Widgets.Models;
using BookLoggerApp.Platforms.Android.Widgets.Services;

namespace BookLoggerApp.Platforms.Android.Widgets;

/// <summary>
/// Configuration activity for the Daily Goal widget.
/// Lets the user pick which active reading goal to display.
/// Uses native Android views (not Blazor) since this is a lightweight config screen.
/// </summary>
[Activity(Label = "Configure Widget", Exported = true,
    Name = "com.bookheart.app.WidgetConfigurationActivity",
    Theme = "@android:style/Theme.Material.NoActionBar")]
public class WidgetConfigurationActivity : Activity
{
    private int _appWidgetId = AppWidgetManager.InvalidAppwidgetId;
    private List<GoalWidgetData> _goals = new();
    private int _selectedIndex = -1;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Default result is CANCELED — if user backs out, widget won't be added
        SetResult(Result.Canceled);

        // Get the widget ID from the intent
        _appWidgetId = Intent?.GetIntExtra(AppWidgetManager.ExtraAppwidgetId,
            AppWidgetManager.InvalidAppwidgetId) ?? AppWidgetManager.InvalidAppwidgetId;

        if (_appWidgetId == AppWidgetManager.InvalidAppwidgetId)
        {
            Finish();
            return;
        }

        SetContentView(Resource.Layout.activity_widget_config);

        LoadGoals();
        SetupConfirmButton();
    }

    private void LoadGoals()
    {
        try
        {
            _goals = Task.Run(() => WidgetDataService.GetActiveGoalDataAsync()).GetAwaiter().GetResult();
        }
        catch
        {
            _goals = new List<GoalWidgetData>();
        }

        var listView = FindViewById<global::Android.Widget.ListView>(Resource.Id.widget_config_goal_list);
        var emptyView = FindViewById<global::Android.Widget.TextView>(Resource.Id.widget_config_empty);

        if (_goals.Count == 0)
        {
            if (listView is not null) listView.Visibility = ViewStates.Gone;
            if (emptyView is not null) emptyView.Visibility = ViewStates.Visible;
            return;
        }

        if (emptyView is not null) emptyView.Visibility = ViewStates.Gone;

        // Build display strings
        var goalLabels = _goals.Select(g =>
        {
            var unit = g.GoalType switch
            {
                "Books" => "Books",
                "Pages" => "Pages",
                "Minutes" => "Minutes",
                _ => ""
            };
            return $"{g.Title}\n{g.Current}/{g.Target} {unit} — {g.ProgressPercentage}%";
        }).ToArray();

        var adapter = new ArrayAdapter<string>(this,
            global::Android.Resource.Layout.SimpleListItemSingleChoice, goalLabels);

        if (listView is not null)
        {
            listView.Adapter = adapter;
            listView.ChoiceMode = ChoiceMode.Single;
            listView.SetItemChecked(0, true);
            _selectedIndex = 0;

            listView.ItemClick += (_, args) =>
            {
                _selectedIndex = args.Position;
            };
        }
    }

    private void SetupConfirmButton()
    {
        var button = FindViewById<global::Android.Widget.Button>(Resource.Id.widget_config_confirm);
        if (button is null) return;

        button.Click += (_, _) =>
        {
            if (_selectedIndex < 0 || _selectedIndex >= _goals.Count)
            {
                // No goal selected — save without specific goal (will use first active)
                SaveConfigAndFinish(null);
            }
            else
            {
                SaveConfigAndFinish(_goals[_selectedIndex].GoalId);
            }
        };
    }

    private void SaveConfigAndFinish(Guid? goalId)
    {
        // Save selected goal ID to SharedPreferences
        var prefs = GetSharedPreferences(DailyGoalWidgetProvider.PrefsName, FileCreationMode.Private);
        var editor = prefs?.Edit();
        if (editor is not null)
        {
            if (goalId.HasValue)
                editor.PutString($"{DailyGoalWidgetProvider.PrefKeyGoalIdPrefix}{_appWidgetId}",
                    goalId.Value.ToString());
            else
                editor.Remove($"{DailyGoalWidgetProvider.PrefKeyGoalIdPrefix}{_appWidgetId}");

            editor.Apply();
        }

        // Trigger initial widget update
        var appWidgetManager = AppWidgetManager.GetInstance(this);
        if (appWidgetManager is not null)
        {
            DailyGoalWidgetProvider.UpdateWidget(this, appWidgetManager, _appWidgetId);
        }

        // Return success
        var resultIntent = new Intent();
        resultIntent.PutExtra(AppWidgetManager.ExtraAppwidgetId, _appWidgetId);
        SetResult(Result.Ok, resultIntent);
        Finish();
    }
}
