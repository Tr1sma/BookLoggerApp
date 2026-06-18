using BookLoggerApp.Core.Services.Abstractions;

#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
#endif

namespace BookLoggerApp.Services;

public sealed class AppRestartService : IAppRestartService
{
    public void RestartApp()
    {
#if ANDROID
        if (!Microsoft.Maui.ApplicationModel.MainThread.IsMainThread)
        {
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(RestartApp);
            return;
        }

        // Three-strategy restart for Android 7–15.
        // Android 12+ BAL restrictions block AlarmManager-only restarts when the source
        // process is already dead. Strategy 1 (direct StartActivity while still in
        // foreground) bypasses BAL; Strategy 2 is a fallback for OEMs that silently drop
        // it; Strategy 3 gives the IPC time to commit before the process dies.
        var context = Android.App.Application.Context;
        var packageManager = context.PackageManager;
        var launchIntent = packageManager?.GetLaunchIntentForPackage(context.PackageName!);
        if (launchIntent is null)
        {
            System.Diagnostics.Debug.WriteLine("AppRestart: no launch intent — aborting");
            return;
        }

        launchIntent.AddFlags(
            ActivityFlags.NewTask |
            ActivityFlags.ClearTask |
            ActivityFlags.ClearTop);

        // Strategy 1: foreground StartActivity
        try
        {
            context.StartActivity(launchIntent);
            System.Diagnostics.Debug.WriteLine("AppRestart: direct StartActivity dispatched");
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppRestart: StartActivity threw: {ex}");
        }

        // Strategy 2: AlarmManager fallback (no SCHEDULE_EXACT_ALARM required)
        try
        {
            var pendingIntentFlags = PendingIntentFlags.OneShot;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                pendingIntentFlags |= PendingIntentFlags.Immutable;
            }

            var pendingIntent = PendingIntent.GetActivity(
                context,
                0,
                launchIntent,
                pendingIntentFlags);

            var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
            if (alarmManager is not null && pendingIntent is not null)
            {
                var triggerAt = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 500;
                if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
                {
                    alarmManager.SetAndAllowWhileIdle(AlarmType.Rtc, triggerAt, pendingIntent);
                }
                else
                {
                    alarmManager.Set(AlarmType.Rtc, triggerAt, pendingIntent);
                }
                System.Diagnostics.Debug.WriteLine("AppRestart: alarm fallback scheduled");
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppRestart: alarm fallback threw: {ex}");
        }

        // Strategy 3: 300ms pause so IPC commits before process dies
        try
        {
            System.Threading.Thread.Sleep(300);
        }
        catch
        {
            // Interrupted — proceed to kill.
        }

        Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
        Java.Lang.JavaSystem.Exit(0);
#endif
    }
}
