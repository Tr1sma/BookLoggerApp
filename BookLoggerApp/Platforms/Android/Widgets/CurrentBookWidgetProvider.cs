using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Graphics;
using Android.Widget;
using BookLoggerApp.Platforms.Android.Widgets.Services;

namespace BookLoggerApp.Platforms.Android.Widgets;

[BroadcastReceiver(Label = "Current Book", Exported = true)]
[IntentFilter(new[] { "android.appwidget.action.APPWIDGET_UPDATE" })]
[MetaData("android.appwidget.provider", Resource = "@xml/widget_current_book_info")]
public class CurrentBookWidgetProvider : AppWidgetProvider
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
        var views = new RemoteViews(context.PackageName, Resource.Layout.widget_current_book);

        try
        {
            var bookData = Task.Run(() => WidgetDataService.GetCurrentBookDataAsync()).GetAwaiter().GetResult();

            if (bookData is not null)
            {
                views.SetTextViewText(Resource.Id.widget_book_title, bookData.Title);
                views.SetTextViewText(Resource.Id.widget_book_author, bookData.Author);
                views.SetProgressBar(Resource.Id.widget_progress_bar, 100, bookData.ProgressPercentage, false);
                views.SetTextViewText(Resource.Id.widget_page_info,
                    $"Page {bookData.CurrentPage}/{bookData.TotalPages} — {bookData.ProgressPercentage}%");

                if (bookData.CoverImagePath is not null && File.Exists(bookData.CoverImagePath))
                {
                    var bitmap = LoadScaledBitmap(bookData.CoverImagePath, 168, 252);
                    if (bitmap is not null)
                    {
                        views.SetImageViewBitmap(Resource.Id.widget_cover_image, bitmap);
                    }
                }
                else
                {
                    views.SetImageViewResource(Resource.Id.widget_cover_image, Resource.Drawable.widget_placeholder_cover);
                }
            }
            else
            {
                views.SetTextViewText(Resource.Id.widget_book_title, "No active book");
                views.SetTextViewText(Resource.Id.widget_book_author, "");
                views.SetProgressBar(Resource.Id.widget_progress_bar, 100, 0, false);
                views.SetTextViewText(Resource.Id.widget_page_info, "Tap to open BookHeart");
                views.SetImageViewResource(Resource.Id.widget_cover_image, Resource.Drawable.widget_placeholder_cover);
            }
        }
        catch
        {
            views.SetTextViewText(Resource.Id.widget_book_title, "BookHeart");
            views.SetTextViewText(Resource.Id.widget_book_author, "");
            views.SetTextViewText(Resource.Id.widget_page_info, "Tap to open");
        }

        var intent = new Intent(context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
        var pendingIntent = PendingIntent.GetActivity(
            context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        views.SetOnClickPendingIntent(Resource.Id.widget_current_book_root, pendingIntent);

        appWidgetManager.UpdateAppWidget(widgetId, views);
    }

    // InSampleSize avoids TransactionTooLargeException in RemoteViews
    private static Bitmap? LoadScaledBitmap(string path, int maxWidth, int maxHeight)
    {
        try
        {
            var boundsOptions = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeFile(path, boundsOptions);

            int sampleSize = 1;
            if (boundsOptions.OutHeight > maxHeight || boundsOptions.OutWidth > maxWidth)
            {
                int halfHeight = boundsOptions.OutHeight / 2;
                int halfWidth = boundsOptions.OutWidth / 2;

                while ((halfHeight / sampleSize) >= maxHeight && (halfWidth / sampleSize) >= maxWidth)
                {
                    sampleSize *= 2;
                }
            }

            var decodeOptions = new BitmapFactory.Options { InSampleSize = sampleSize };
            return BitmapFactory.DecodeFile(path, decodeOptions);
        }
        catch
        {
            return null;
        }
    }
}
