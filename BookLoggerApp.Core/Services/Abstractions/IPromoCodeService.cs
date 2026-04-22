using BookLoggerApp.Core.Entitlements;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Validates in-app promo codes. Hardcoded short-term codes (prefix <c>BH-</c>)
/// grant a temporary Plus or Premium window. Google Play native promo codes for
/// high-value rewards (e.g. Lifetime Premium) are redeemed in the Play Store and
/// flow back through <see cref="IBillingService"/>, not this service.
/// </summary>
public interface IPromoCodeService
{
    Task<PromoCodeRedemptionResult> RedeemAsync(string code, CancellationToken ct = default);
}

public record PromoCodeRedemptionResult(
    bool Success,
    string Message,
    PromoActivation? Activation = null);
