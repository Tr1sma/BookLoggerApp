using Microsoft.AspNetCore.Components.WebView.Maui;

using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp
{
    public partial class MainPage : ContentPage
    {
        private IAdService? _adService;

        public MainPage()
        {
            System.Diagnostics.Debug.WriteLine("=== MainPage Constructor Started ===");
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("=== MainPage InitializeComponent Completed ===");

            if (this.FindByName<BlazorWebView>("blazorWebView") is BlazorWebView webView)
            {
                System.Diagnostics.Debug.WriteLine("=== BlazorWebView found, attaching handlers ===");
            }
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();

            if (Handler?.MauiContext?.Services is IServiceProvider services)
            {
                _adService = services.GetService<IAdService>();
                if (_adService != null)
                {
                    _adService.BannerVisibilityChanged += OnBannerVisibilityChanged;
                    SetAdBannerVisibility(_adService.IsBannerVisible);
                }
            }
        }

        private void OnBannerVisibilityChanged(object? sender, bool isVisible)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SetAdBannerVisibility(isVisible);
            });
        }

        private void SetAdBannerVisibility(bool isVisible)
        {
            var banner = this.FindByName<View>("adBanner");
            if (banner != null)
            {
                banner.IsVisible = isVisible;
                System.Diagnostics.Debug.WriteLine($"=== Ad Banner Visibility: {isVisible} ===");
            }
        }
    }
}
