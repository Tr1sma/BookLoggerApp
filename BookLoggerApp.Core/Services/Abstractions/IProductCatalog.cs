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

    /// <summary>
    /// True only when the SKU for <paramref name="tier"/>+<paramref name="period"/> actually has a
    /// configured Play Console introductory offer. Drives the paywall "first month" badge so it is
    /// never shown for a tier that has no intro offer (CODE_REVIEW LOG-07).
    /// </summary>
    bool HasIntroductoryOffer(SubscriptionTier tier, BillingPeriod period);

    /// <summary>All SKUs the app cares about — handed to <c>IBillingService.QueryProductsAsync</c>.</summary>
    IReadOnlyList<string> AllProductIds { get; }
}
