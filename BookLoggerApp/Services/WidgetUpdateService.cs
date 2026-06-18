using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Services;

/// <summary>Triggers Android widget refresh after data changes. No-op elsewhere.</summary>
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
            // Best-effort — don't crash.
        }
#endif
    }
}
