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
        // Use AlarmManager + PendingIntent to relaunch the app. On Android 12+
        // a direct context.StartActivity(...) from a dying process is unreliable
        // because background activity starts are restricted. The system
        // AlarmManager, being a trusted service, can launch the activity even
        // after the current process is killed via JavaSystem.Exit.
        var context = Android.App.Application.Context;
        var packageManager = context.PackageManager;
        var launchIntent = packageManager?.GetLaunchIntentForPackage(context.PackageName!);
        if (launchIntent is null)
        {
            return;
        }

        launchIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);

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
        if (alarmManager is null || pendingIntent is null)
        {
            return;
        }

        var triggerAt = DateTimeOffset.Now.ToUnixTimeMilliseconds() + 100;

        // SetAndAllowWhileIdle works without SCHEDULE_EXACT_ALARM on Android 12+
        // and still fires within a few hundred ms for our near-zero delay.
        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            alarmManager.SetAndAllowWhileIdle(AlarmType.Rtc, triggerAt, pendingIntent);
        }
        else
        {
            alarmManager.Set(AlarmType.Rtc, triggerAt, pendingIntent);
        }

        Java.Lang.JavaSystem.Exit(0);
#endif
    }
}
