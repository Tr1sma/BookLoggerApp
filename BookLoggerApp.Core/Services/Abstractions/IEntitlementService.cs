using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// App-wide singleton that holds the current subscription tier in-memory, persists
/// changes, and broadcasts <see cref="EntitlementChanged"/> so UI can re-render.
/// Consumed by every feature gate in ViewModels, Services, and Blazor components.
/// </summary>
public interface IEntitlementService
{
    event EventHandler<EntitlementChangedEventArgs>? EntitlementChanged;

    /// <summary>Cached current tier. Returns <c>Free</c> until <see cref="InitializeAsync"/> has completed.</summary>
    SubscriptionTier CurrentTier { get; }

    /// <summary>Cached snapshot of the current entitlement row. Null until initialized.</summary>
    UserEntitlement? CurrentEntitlement { get; }

    bool IsInitialized { get; }

    /// <summary>
    /// Loads the entitlement row into memory. Idempotent: safe to call more than once.
    /// Should be invoked from the app startup flow after DB init.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Synchronous gate check. Returns <c>false</c> if the service has not yet
    /// initialized — callers that render critical UI should await <see cref="InitializeAsync"/> first.
    /// </summary>
    bool HasAccess(FeatureKey feature);

    /// <summary>
    /// Awaitable variant: ensures the service is initialized before checking.
    /// </summary>
    Task<bool> HasAccessAsync(FeatureKey feature, CancellationToken ct = default);

    /// <summary>
    /// Reloads the entitlement row from the database, re-evaluates expiry, and
    /// fires <see cref="EntitlementChanged"/> if the tier moved.
    /// </summary>
    Task RefreshAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists a successful purchase (or restore) and transitions to the new tier.
    /// </summary>
    Task ApplyPurchaseAsync(PurchaseResult purchase, EntitlementChangeReason reason = EntitlementChangeReason.Purchase, CancellationToken ct = default);

    /// <summary>
    /// Persists a lapse event (expiry/cancel/refund) and downgrades to Free.
    /// </summary>
    Task ApplyLapseAsync(string reason, CancellationToken ct = default);

    /// <summary>
    /// Persists a hardcoded in-app promo-code redemption.
    /// </summary>
    Task ApplyPromoAsync(PromoActivation promo, CancellationToken ct = default);

    /// <summary>
    /// Development override for local testing — bypasses Play Billing entirely.
    /// Release builds should not expose UI that calls this, but the implementation
    /// remains functional so that QA/dev panels keep working.
    /// </summary>
    Task ForceTierForDebugAsync(SubscriptionTier tier, CancellationToken ct = default);
}
