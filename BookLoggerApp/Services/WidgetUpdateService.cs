using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Services;

/// <summary>
/// Triggers Android widget refresh after data changes (reading sessions, goals, book progress).
/// No-op on non-Android platforms.
/// </summary>
public class WidgetUpdateService : IWidgetUpdateService
{
    public void NotifyDataChanged()
    {
#if ANDROID
        try
        {
            var context = global::Android.App.Application.Context;
            Platforms.Android.Widgets.WidgetUpdateHelper.UpdateAllWidgets(context);
        }
        catch
        {
            // Widget update is best-effort — don't crash the app
        }
#endif
    }
}
