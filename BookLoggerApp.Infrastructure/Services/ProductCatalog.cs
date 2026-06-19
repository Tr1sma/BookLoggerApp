using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Static product catalog. Play Console must have these SKUs configured:
/// <list type="bullet">
/// <item><c>plus_monthly</c> and <c>plus_yearly</c> (subscription)</item>
/// <item><c>premium_monthly</c> and <c>premium_yearly</c> (subscription, family-sharing on)</item>
/// <item><c>premium_lifetime</c> (managed product)</item>
/// </list>
/// </summary>
public class ProductCatalog : IProductCatalog
{
    public const string PlusMonthly = "plus_monthly";
    public const string PlusYearly = "plus_yearly";
    public const string PremiumMonthly = "premium_monthly";
    public const string PremiumYearly = "premium_yearly";
    public const string PremiumLifetime = "premium_lifetime";

    private static readonly Dictionary<(SubscriptionTier, BillingPeriod), string> _forward = new()
    {
        [(SubscriptionTier.Plus, BillingPeriod.Monthly)] = PlusMonthly,
        [(SubscriptionTier.Plus, BillingPeriod.Yearly)] = PlusYearly,
        [(SubscriptionTier.Premium, BillingPeriod.Monthly)] = PremiumMonthly,
        [(SubscriptionTier.Premium, BillingPeriod.Yearly)] = PremiumYearly,
        [(SubscriptionTier.Premium, BillingPeriod.Lifetime)] = PremiumLifetime
    };

    private static readonly Dictionary<string, (SubscriptionTier, BillingPeriod)> _reverse =
        _forward.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// SKUs that have a confirmed Play Console introductory offer. Intentionally empty until a
    /// real intro offer is configured: the paywall must not advertise a "first month" price on a
    /// SKU that does not have one (CODE_REVIEW LOG-07). Add the SKU constant here once verified.
    /// </summary>
    private static readonly HashSet<string> _introOfferSkus = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> AllProductIds { get; } = _forward.Values.ToArray();

    public string? GetProductId(SubscriptionTier tier, BillingPeriod period)
    {
        return _forward.TryGetValue((tier, period), out string? sku) ? sku : null;
    }

    public (SubscriptionTier Tier, BillingPeriod Period)? TryResolve(string productId)
    {
        return _reverse.TryGetValue(productId, out (SubscriptionTier Tier, BillingPeriod Period) pair)
            ? pair
            : null;
    }

    public bool HasIntroductoryOffer(SubscriptionTier tier, BillingPeriod period)
    {
        string? sku = GetProductId(tier, period);
        return sku is not null && _introOfferSkus.Contains(sku);
    }
}
