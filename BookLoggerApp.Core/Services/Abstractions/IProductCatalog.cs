using BookLoggerApp.Core.Entitlements;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Maps (tier × billing period) to the Google Play SKU identifier. Keeps the
/// SKU string table in one place so paywall UI, billing service, and promo
/// redemption all speak the same language.
/// </summary>
public interface IProductCatalog
{
    /// <summary>Returns the SKU for <paramref name="tier"/>+<paramref name="period"/>, or null if not offered.</summary>
    string? GetProductId(SubscriptionTier tier, BillingPeriod period);

    /// <summary>Reverse lookup: resolve a SKU back to tier+period. Returns null for unknown SKUs.</summary>
    (SubscriptionTier Tier, BillingPeriod Period)? TryResolve(string productId);

    /// <summary>All SKUs the app cares about — handed to <c>IBillingService.QueryProductsAsync</c>.</summary>
    IReadOnlyList<string> AllProductIds { get; }
}
