namespace BookLoggerApp.Services;

/// <summary>
/// AdMob configuration. Gitignored — copy from template and fill in production IDs.
/// Test IDs are pre-filled for development.
/// </summary>
internal static class AdConfiguration
{
    /// <summary>AdMob App ID. Test: ca-app-pub-3940256099942544~3347511713</summary>
    internal const string AdMobAppId = "ca-app-pub-3940256099942544~3347511713";

    /// <summary>Banner Ad Unit ID. Test: ca-app-pub-3940256099942544/6300978111</summary>
    internal const string BannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";

    /// <summary>Set false in production to use real ad unit IDs.</summary>
    internal const bool UseTestAds = true;
}
