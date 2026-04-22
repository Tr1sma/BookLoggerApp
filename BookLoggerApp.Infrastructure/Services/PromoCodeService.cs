using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Validates hardcoded <c>BH-</c>-prefixed promo codes. Codes are declared in
/// <see cref="HardcodedCodes"/> as (code, grant) pairs; new codes ship in
/// app updates. Play-native promo codes (for single-use Lifetime rewards) are
/// handled by <see cref="IBillingService.LaunchRedeemPromoFlowAsync"/>.
/// </summary>
public class PromoCodeService : IPromoCodeService
{
    private readonly IEntitlementService _entitlementService;

    public PromoCodeService(IEntitlementService entitlementService)
    {
        _entitlementService = entitlementService;
    }

    private static readonly IReadOnlyDictionary<string, PromoGrant> HardcodedCodes =
        new Dictionary<string, PromoGrant>(StringComparer.OrdinalIgnoreCase)
        {
            ["BH-BETA2026"] = new(SubscriptionTier.Plus, BillingPeriod.Monthly, 30),
            ["BH-LAUNCH"]    = new(SubscriptionTier.Plus, BillingPeriod.Monthly, 90),
            ["BH-VIP"]       = new(SubscriptionTier.Premium, BillingPeriod.Monthly, 30)
        };

    public async Task<PromoCodeRedemptionResult> RedeemAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new PromoCodeRedemptionResult(false, "Please enter a promo code.");
        }

        string trimmed = code.Trim();

        if (!HardcodedCodes.TryGetValue(trimmed, out PromoGrant? grant))
        {
            return new PromoCodeRedemptionResult(false, "Unknown promo code.");
        }

        DateTime expiresAt = DateTime.UtcNow.AddDays(grant.DurationDays);
        PromoActivation activation = new(grant.Tier, grant.Period, trimmed, expiresAt);

        await _entitlementService.ApplyPromoAsync(activation, ct);

        string duration = grant.DurationDays switch
        {
            30 => "30 days",
            90 => "3 months",
            _ => $"{grant.DurationDays} days"
        };
        return new PromoCodeRedemptionResult(true, $"{grant.Tier} unlocked for {duration}.", activation);
    }

    private sealed record PromoGrant(SubscriptionTier Tier, BillingPeriod Period, int DurationDays);
}
