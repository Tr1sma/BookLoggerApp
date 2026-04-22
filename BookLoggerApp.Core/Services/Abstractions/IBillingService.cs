using BookLoggerApp.Core.Entitlements;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Platform wrapper around Google Play Billing Library (Android) / no-op (other platforms).
/// Step 7 scaffolding — real Android implementation is wired in Step 8 once the
/// <c>Plugin.Maui.InAppBilling</c> NuGet is added.
/// </summary>
public interface IBillingService
{
    bool IsConnected { get; }

    event EventHandler<PurchaseResult>? PurchaseUpdated;

    Task<bool> ConnectAsync(CancellationToken ct = default);

    Task DisconnectAsync();

    Task<IReadOnlyList<BillingProduct>> QueryProductsAsync(IEnumerable<string> productIds, CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseResult>> QueryActivePurchasesAsync(CancellationToken ct = default);

    Task<BillingPurchaseOutcome> LaunchPurchaseFlowAsync(string productId, string? oldPurchaseToken = null, CancellationToken ct = default);

    Task AcknowledgePurchaseAsync(string purchaseToken, CancellationToken ct = default);

    Task<bool> LaunchRedeemPromoFlowAsync(CancellationToken ct = default);

    Task<bool> OpenSubscriptionManagementAsync(string? productId = null, CancellationToken ct = default);
}

/// <summary>Product metadata returned by Play: SKU, localized title, formatted price, billing cadence.</summary>
public record BillingProduct(
    string ProductId,
    string Title,
    string Description,
    string FormattedPrice,
    SubscriptionTier Tier,
    BillingPeriod Period,
    bool HasIntroOffer,
    string? IntroFormattedPrice);

public enum BillingPurchaseOutcome
{
    Success = 0,
    UserCancelled = 1,
    AlreadyOwned = 2,
    NotAvailable = 3,
    BillingUnavailable = 4,
    Error = 5
}
