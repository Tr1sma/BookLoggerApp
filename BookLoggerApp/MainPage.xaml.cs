using Microsoft.AspNetCore.Components.WebView.Maui;

namespace BookLoggerApp
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            System.Diagnostics.Debug.WriteLine("=== MainPage Constructor Started ===");
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("=== MainPage InitializeComponent Completed ===");
            
            // Add handler for BlazorWebView events
            if (this.FindByName<BlazorWebView>("blazorWebView") is BlazorWebView webView)
            {
                System.Diagnostics.Debug.WriteLine("=== BlazorWebView found, attaching handlers ===");
            }
        }
    }
}
