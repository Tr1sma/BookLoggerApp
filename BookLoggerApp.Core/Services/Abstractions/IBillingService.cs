using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Handles in-app purchase operations via Google Play Billing.
/// Implemented in the MAUI project with platform-specific code.
/// </summary>
public interface IBillingService
{
    /// <summary>
    /// Retrieves available subscription products from the store.
    /// </summary>
    Task<IReadOnlyList<ProductInfo>> GetProductsAsync(CancellationToken ct = default);

    /// <summary>
    /// Initiates a purchase flow for the given product.
    /// On success, automatically updates the subscription tier via ISubscriptionService.
    /// </summary>
    Task<PurchaseResult> PurchaseAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// Restores previously purchased subscriptions from the store.
    /// Updates subscription tier to Premium if an active subscription is found, or Free if not.
    /// </summary>
    Task<bool> RestorePurchasesAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks whether the user has an active subscription in the store.
    /// </summary>
    Task<bool> HasActiveSubscriptionAsync(CancellationToken ct = default);
}
