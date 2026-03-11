namespace BookLoggerApp.Core.Constants;

/// <summary>
/// Product IDs and billing configuration for Google Play subscriptions.
/// </summary>
public static class BillingConstants
{
    public const string PremiumMonthly = "premium_monthly";
    public const string PremiumYearly = "premium_yearly";

    public static readonly string[] AllProductIds = [PremiumMonthly, PremiumYearly];

    /// <summary>
    /// Returns the estimated subscription duration for a product ID.
    /// This is a client-side approximation; the actual expiry is managed by Google Play.
    /// Startup restore re-validates on each app launch.
    /// </summary>
    public static TimeSpan GetEstimatedDuration(string productId) => productId switch
    {
        PremiumMonthly => TimeSpan.FromDays(30),
        PremiumYearly => TimeSpan.FromDays(365),
        _ => TimeSpan.FromDays(30)
    };
}
