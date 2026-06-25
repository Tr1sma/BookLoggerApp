#if ANDROID
using Android.OS;

namespace BookLoggerApp.Platforms.AndroidImpl.Analytics;

internal static class AndroidAnalyticsExtensions
{
    private const int MaxParamValueLength = 100;
    private const int MaxStringParamKeyLength = 40;

    /// <summary>
    /// Converts a parameter dictionary to an Android Bundle for FirebaseAnalytics.LogEvent.
    /// Strings over Firebase's 100-char limit are truncated; null values skipped.
    /// </summary>
    public static Bundle ToBundle(this IDictionary<string, object?>? parameters)
    {
        var bundle = new Bundle();
        if (parameters is null) return bundle;

        foreach (var kvp in parameters)
        {
            if (kvp.Value is null) continue;
            var key = kvp.Key.Length > MaxStringParamKeyLength
                ? kvp.Key.Substring(0, MaxStringParamKeyLength)
                : kvp.Key;

            switch (kvp.Value)
            {
                case string s:
                    bundle.PutString(key, s.Length > MaxParamValueLength ? s.Substring(0, MaxParamValueLength) : s);
                    break;
                case bool b:
                    bundle.PutLong(key, b ? 1L : 0L);
                    break;
                case int i:
                    bundle.PutLong(key, i);
                    break;
                case long l:
                    bundle.PutLong(key, l);
                    break;
                case double d:
                    bundle.PutDouble(key, d);
                    break;
                case float f:
                    bundle.PutDouble(key, f);
                    break;
                default:
                    var str = kvp.Value.ToString() ?? string.Empty;
                    bundle.PutString(key, str.Length > MaxParamValueLength ? str.Substring(0, MaxParamValueLength) : str);
                    break;
            }
        }

        return bundle;
    }

    /// <summary>
    /// Wraps an <see cref="Exception"/> in a Java <see cref="Java.Lang.Throwable"/> for
    /// Crashlytics.RecordException, preserving the C# stack trace across the Java hop.
    /// </summary>
    public static Java.Lang.Throwable ToThrowable(this Exception exception)
    {
        if (exception is null) throw new ArgumentNullException(nameof(exception));
        var wrapper = new Java.Lang.Throwable(
            $"[{exception.GetType().FullName}] {exception.Message}\n{exception.StackTrace}");
        return wrapper;
    }
}
#endif
