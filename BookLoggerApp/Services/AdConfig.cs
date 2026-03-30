namespace BookLoggerApp.Services;

/// <summary>
/// AdMob configuration. This file is gitignored — do not check in.
/// Copy this file to AdConfig.cs and fill in your IDs for production.
/// For development, test IDs are pre-filled.
/// </summary>
internal static class AdConfiguration
{
    /// <summary>
    /// AdMob Application ID. Use test App ID during development.
    /// Android test App ID: ca-app-pub-3940256099942544~3347511713
    /// </summary>
    internal const string AdMobAppId = "ca-app-pub-3940256099942544~3347511713";

    /// <summary>
    /// Banner Ad Unit ID. Use test Ad Unit ID during development.
    /// Android test Banner ID: ca-app-pub-3940256099942544/6300978111
    /// </summary>
    internal const string BannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";

    /// <summary>
    /// Set to true during development to use Google's test ad unit IDs.
    /// Set to false in production with real ad unit IDs above.
    /// </summary>
    internal const bool UseTestAds = true;
}
