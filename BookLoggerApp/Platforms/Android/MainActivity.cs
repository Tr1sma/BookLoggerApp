using Android.App;
using Android.Content.PM;
using Android.OS;

namespace BookLoggerApp
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
            // Use AndroidX OnBackPressedDispatcher which works for all Android versions
            // and integrates with MAUI/AppCompat correctly.
            OnBackPressedDispatcher.AddCallback(this, new BackPressCallback(this));
            
            System.Diagnostics.Debug.WriteLine("=== MainActivity: Registered AndroidX BackPressCallback ===");
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
