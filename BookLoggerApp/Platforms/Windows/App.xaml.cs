using Microsoft.UI.Xaml;

namespace BookLoggerApp.WinUI
{
    public partial class App : MauiWinUIApplication
    {
        public App()
        {
            System.Diagnostics.Debug.WriteLine("=== Windows App Constructor Started ===");
            UnhandledException += OnUnhandledException;
            
            this.InitializeComponent();
            
            System.Diagnostics.Debug.WriteLine("=== Windows App InitializeComponent Completed ===");
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("=== UNHANDLED EXCEPTION IN WINDOWS APP ===");
            System.Diagnostics.Debug.WriteLine($"Exception Type: {e.Exception.GetType().FullName}");
            System.Diagnostics.Debug.WriteLine($"Message: {e.Exception.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {e.Exception.StackTrace}");
            
            if (e.Exception.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {e.Exception.InnerException.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"Inner Message: {e.Exception.InnerException.Message}");
                System.Diagnostics.Debug.WriteLine($"Inner Stack: {e.Exception.InnerException.StackTrace}");
            }
            
            System.Diagnostics.Debug.WriteLine($"Handled: {e.Handled}");
            System.Diagnostics.Debug.WriteLine("=== END UNHANDLED EXCEPTION ===");
            
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
