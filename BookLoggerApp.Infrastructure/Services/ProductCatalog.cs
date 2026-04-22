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
}
