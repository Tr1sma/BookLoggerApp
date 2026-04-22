namespace BookLoggerApp.Core.Entitlements;

/// <summary>
/// Platform-neutral description of a successful in-app promo-code redemption.
/// Consumed by <c>IEntitlementService.ApplyPromoAsync</c>. Play-native promo codes
/// are redeemed in the Play Store and flow back through <c>PurchaseResult</c>, not
/// this path.
/// </summary>
public record PromoActivation(
    SubscriptionTier GrantedTier,
    BillingPeriod GrantedPeriod,
    string Code,
    DateTime? ExpiresAt);
