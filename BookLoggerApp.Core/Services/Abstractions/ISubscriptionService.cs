using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Manages subscription tier status and feature gating.
/// Registered as singleton; caches tier locally.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Raised when the subscription tier changes (e.g., Free → Premium or vice versa).
    /// </summary>
    event EventHandler? TierChanged;

    /// <summary>
    /// Gets the current subscription tier.
    /// Returns cached value if available; falls back to Free if no data exists.
    /// Auto-downgrades to Free if the subscription has expired.
    /// </summary>
    Task<SubscriptionTier> GetCurrentTierAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks whether the user has access to a specific feature based on their tier.
    /// </summary>
    Task<bool> HasFeatureAccessAsync(FeatureFlag flag, CancellationToken ct = default);

    /// <summary>
    /// Attempts to restore purchases from the store.
    /// Stub until Issue #2 (billing integration) is implemented.
    /// </summary>
    Task RestorePurchasesAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates the subscription tier. Called by billing integration (Issue #2).
    /// </summary>
    Task UpdateTierAsync(SubscriptionTier tier, string? productId = null,
        DateTime? expiresAt = null, string? purchaseToken = null,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidates the cached subscription info, forcing a reload from database.
    /// </summary>
    void InvalidateCache();
}
