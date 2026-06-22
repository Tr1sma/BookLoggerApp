using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace BookLoggerApp
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Register notification channels with proper importance before any notifications are scheduled.
            // Without this, auto-created channels default to IMPORTANCE_DEFAULT which won't wake the device.
            CreateNotificationChannels();

            // Use AndroidX OnBackPressedDispatcher which works for all Android versions
            // and integrates with MAUI/AppCompat correctly.
            OnBackPressedDispatcher.AddCallback(this, new BackPressCallback(this));

            System.Diagnostics.Debug.WriteLine("=== MainActivity: Registered AndroidX BackPressCallback ===");

            InitializeFirebase();

            // Cold-start deep link (e.g. launched by tapping the reading-timer notification).
            HandleTimerDeepLink(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            // Keep getIntent() current and route the deep link while the app is already running.
            Intent = intent;
            HandleTimerDeepLink(intent);
        }

        private void HandleTimerDeepLink(Intent? intent)
        {
            var bookId = intent?.GetStringExtra(
                BookLoggerApp.Platforms.Android.Services.ReadingTimerForegroundService.ExtraBookId);
            if (string.IsNullOrEmpty(bookId))
                return;

            var services = Microsoft.Maui.IPlatformApplication.Current?.Services;

            // Stop button: pause the running session first so the user lands on the book page
            // with the timer stopped and the save UI ready (they confirm the page and save).
            bool stopRequested = intent!.GetBooleanExtra(
                BookLoggerApp.Platforms.Android.Services.ReadingTimerForegroundService.ExtraStopRequested, false);
            if (stopRequested)
            {
                var timerState = services?.GetService<BookLoggerApp.Core.Services.Abstractions.ITimerStateService>();
                PauseTimerForStop(timerState);
                // Pause a currently-mounted inline timer immediately (no-op on cold start —
                // the persisted paused state above drives the restore instead).
                timerState?.NotifyExternalCommand(BookLoggerApp.Core.Services.Abstractions.ExternalTimerCommand.Pause);
            }

            var deepLink = services?.GetService<BookLoggerApp.Core.Services.Abstractions.IDeepLinkService>();
            // Open the book's detail page (the inline reading timer lives there). DeepLinkService
            // buffers the route until Routes.razor subscribes (cold start).
            deepLink?.RequestNavigation($"/books/{bookId}");
        }

        /// <summary>
        /// Converts a running persisted timer state to paused so the inline timer restores in
        /// its stopped/save-ready state. No-op if nothing is running (already paused or none).
        /// </summary>
        private static void PauseTimerForStop(BookLoggerApp.Core.Services.Abstractions.ITimerStateService? timerState)
        {
            if (timerState is null)
                return;

            var state = timerState.LoadState();
            if (state is null || !state.IsRunning)
                return;

            var elapsed = System.DateTime.UtcNow - state.StartTimeUtc;
            if (elapsed < System.TimeSpan.Zero)
                elapsed = System.TimeSpan.Zero;

            timerState.SaveState(new BookLoggerApp.Core.Services.Abstractions.TimerStateData
            {
                SessionId = state.SessionId,
                BookId = state.BookId,
                StartTimeTicks = state.StartTimeTicks,
                IsRunning = false,
                PausedElapsedTicks = elapsed.Ticks
            });
        }

        private void InitializeFirebase()
        {
            try
            {
                Firebase.FirebaseApp.InitializeApp(this);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Firebase.InitializeApp failed: {ex}");
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var services = Microsoft.Maui.IPlatformApplication.Current?.Services;
                    var gate = services?.GetService<BookLoggerApp.Core.Services.Abstractions.IAnalyticsConsentGate>();
                    var analytics = services?.GetService<BookLoggerApp.Core.Services.Abstractions.IAnalyticsService>();
                    var crash = services?.GetService<BookLoggerApp.Core.Services.Abstractions.ICrashReportingService>();

                    if (gate is not null)
                    {
                        await gate.InitializeAsync().ConfigureAwait(false);
                    }

                    RunOnUiThread(() =>
                    {
                        try
                        {
                            // Fail-closed fallback: if the gate could not be resolved, keep
                            // collection OFF rather than ON (mirrors the gate's own fail-closed
                            // default and the manifest defaults). See code review SEC-01.
                            analytics?.SetAnalyticsCollectionEnabled(gate?.AnalyticsAllowed ?? false);
                            crash?.SetCrashlyticsCollectionEnabled(gate?.CrashAllowed ?? false);

                            const string envKey = "environment";
#if DEBUG
                            const string envValue = "debug";
#else
                            const string envValue = "release";
#endif
                            analytics?.SetUserProperty(envKey, envValue);
                            crash?.SetCustomKey(envKey, envValue);
                        }
                        catch (System.Exception innerEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Firebase post-init on UI thread failed: {innerEx}");
                        }
                    });
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Firebase consent init failed: {ex}");
                }
            });
        }

        private void CreateNotificationChannels()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
                return;

            var notificationManager = (Android.App.NotificationManager?)GetSystemService(NotificationService);
            if (notificationManager == null)
                return;

            // Reading reminders: HIGH importance so they wake the device and show heads-up
            var reminderChannel = new Android.App.NotificationChannel(
                "bookheart_reminders",
                "Reading Reminders",
                Android.App.NotificationImportance.High)
            {
                Description = "Daily reading reminder notifications"
            };
            notificationManager.CreateNotificationChannel(reminderChannel);

            // General notifications (goal completed, plant water): DEFAULT importance
            var generalChannel = new Android.App.NotificationChannel(
                "bookheart_general",
                "General",
                Android.App.NotificationImportance.Default)
            {
                Description = "Goal completions, plant notifications"
            };
            notificationManager.CreateNotificationChannel(generalChannel);

            // Reading timer: LOW importance (silent, no heads-up) but persistent and shown
            // on the lock screen — the live reading-timer foreground-service notification.
            var timerChannel = new Android.App.NotificationChannel(
                "bookheart_timer",
                GetString(Resource.String.timer_notif_channel_name),
                Android.App.NotificationImportance.Low)
            {
                Description = GetString(Resource.String.timer_notif_channel_desc),
                LockscreenVisibility = Android.App.NotificationVisibility.Public
            };
            timerChannel.SetShowBadge(false);
            notificationManager.CreateNotificationChannel(timerChannel);

            System.Diagnostics.Debug.WriteLine("=== MainActivity: Notification channels created ===");
        }

        // We do NOT override OnBackPressed anymore, allowing the Dispatcher to handle flow.

        public async Task HandleBackAsync()
        {
            System.Diagnostics.Debug.WriteLine("=== HandleBackAsync Triggered (via AndroidX) ===");
            try
            {
                var services = Microsoft.Maui.IPlatformApplication.Current?.Services;
                var backButtonService = services?.GetService<BookLoggerApp.Core.Services.Abstractions.IBackButtonService>();
                
                System.Diagnostics.Debug.WriteLine($"BackButtonService resolved: {backButtonService != null}");

                if (backButtonService != null)
                {
                    bool handled = await backButtonService.HandleBackAsync();
                    System.Diagnostics.Debug.WriteLine($"BackButtonService Handled: {handled}");
                    
                    if (handled) return;
                }
                else
                {
                     System.Diagnostics.Debug.WriteLine("BackButtonService is NULL");
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling back button: {ex.Message}");
            }
            
            // If we are here, it means we intercepted the back press but decided NOT to handle it (e.g. no modals open).
            // We should let the system perform the default action (minimize app).
            
            System.Diagnostics.Debug.WriteLine("Back Action Not Handled by App -> Minimizing");
            MoveTaskToBack(false);
        }

        private class BackPressCallback : AndroidX.Activity.OnBackPressedCallback
        {
            private readonly MainActivity _activity;

            public BackPressCallback(MainActivity activity) : base(true) // Enabled = true
            {
                _activity = activity;
            }

            public override void HandleOnBackPressed()
            {
                System.Diagnostics.Debug.WriteLine("=== AndroidX HandleOnBackPressed Triggered ===");
                // Fire and forget execution of async logic
                _ = _activity.HandleBackAsync();
            }
        }
    }
}
