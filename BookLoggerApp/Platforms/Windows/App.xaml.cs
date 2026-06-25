using Microsoft.UI.Xaml;

namespace BookLoggerApp.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.
        /// </summary>
        public App()
        {
            System.Diagnostics.Debug.WriteLine("=== Windows App Constructor Started ===");
            
            // Add UnhandledException handler BEFORE InitializeComponent to catch early errors
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
            
            // Don't mark as handled - let the generated code also handle it
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
