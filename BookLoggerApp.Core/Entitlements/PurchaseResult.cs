namespace BookLoggerApp.Core.Entitlements;

/// <summary>
/// Platform-neutral description of a completed Google Play purchase, handed by
/// <c>IBillingService</c> to <c>IEntitlementService.ApplyPurchaseAsync</c>.
/// </summary>
public record PurchaseResult(
    SubscriptionTier Tier,
    BillingPeriod Period,
    string ProductId,
    string PurchaseToken,
    string? OrderId,
    DateTime PurchasedAt,
    DateTime? ExpiresAt,
    bool AutoRenewing,
    bool IsInIntroductoryPrice,
    bool IsFamilyShared);
