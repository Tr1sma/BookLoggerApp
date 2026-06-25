using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>
/// Validates hardcoded <c>BH-</c>-prefixed promo codes from <see cref="HardcodedCodes"/>.
/// Play-native promo codes are handled by <see cref="IBillingService.LaunchRedeemPromoFlowAsync"/>.
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
            return new PromoCodeRedemptionResult(false, "Promo_EnterCode", Array.Empty<object>());
        }

        string trimmed = code.Trim();

        if (!HardcodedCodes.TryGetValue(trimmed, out PromoGrant? grant))
        {
            return new PromoCodeRedemptionResult(false, "Promo_Unknown", Array.Empty<object>());
        }

        DateTime expiresAt = DateTime.UtcNow.AddDays(grant.DurationDays);
        PromoActivation activation = new(grant.Tier, grant.Period, trimmed, expiresAt);

        await _entitlementService.ApplyPromoAsync(activation, ct);

        // UI localizes these keys; use month phrasing for 90 days, else express in days.
        (string messageKey, object[] args) = grant.DurationDays switch
        {
            90 => ("Promo_Success_Months", new object[] { grant.Tier, 3 }),
            _ => ("Promo_Success_Days", new object[] { grant.Tier, grant.DurationDays })
        };
        return new PromoCodeRedemptionResult(true, messageKey, args, activation);
    }

    private sealed record PromoGrant(SubscriptionTier Tier, BillingPeriod Period, int DurationDays);
}
