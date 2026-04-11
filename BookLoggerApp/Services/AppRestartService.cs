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
        // Hybrid restart strategy for Android 7–15. Previously we relied only
        // on an AlarmManager + PendingIntent deferred launch, but Android 12+
        // Background Activity Launch (BAL) restrictions block such launches
        // when the source process is already dead. The fix is to also call
        // StartActivity directly while we're still alive and visible — the
        // foreground context makes BAL allow the launch unconditionally.
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

        // --- Strategy 1: direct foreground StartActivity ---
        // The user just tapped "Restore from Cloud" and Settings is still
        // visible, so the app is in the foreground. Android will either start
        // the Activity immediately in the current process (which we're about
        // to kill) or re-fork a fresh process to host it after we die.
        try
        {
            context.StartActivity(launchIntent);
            System.Diagnostics.Debug.WriteLine("AppRestart: direct StartActivity dispatched");
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppRestart: StartActivity threw: {ex}");
        }

        // --- Strategy 2: AlarmManager fallback ---
        // Only relevant if Strategy 1 is silently dropped (e.g. on an OEM
        // shell with unusual restrictions). SetAndAllowWhileIdle doesn't
        // require SCHEDULE_EXACT_ALARM.
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

        // --- Strategy 3: delayed process kill ---
        // Give Android ~300 ms to commit the StartActivity IPC and finalize
        // the alarm schedule before we kill ourselves. Without this pause the
        // race between "ipc dispatched" and "process dead" can swallow both
        // previous strategies on fast devices.
        try
        {
            System.Threading.Thread.Sleep(300);
        }
        catch
        {
            // Interrupted — don't care, still kill the process below.
        }

        // Kill at both the Linux process level and the JVM level for safety.
        Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
        Java.Lang.JavaSystem.Exit(0);
#endif
    }
}
