using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Entitlements;

/// <summary>
/// Raised by <c>IEntitlementService</c> whenever the active tier changes
/// (purchase, restore, lapse, promo, or debug force). Carries the before/after
/// snapshot so subscribers can run custom diff logic.
/// </summary>
public class EntitlementChangedEventArgs : EventArgs
{
    public SubscriptionTier PreviousTier { get; }

    public SubscriptionTier CurrentTier { get; }

    public UserEntitlement Snapshot { get; }

    public EntitlementChangeReason Reason { get; }

    public EntitlementChangedEventArgs(
        SubscriptionTier previousTier,
        SubscriptionTier currentTier,
        UserEntitlement snapshot,
        EntitlementChangeReason reason)
    {
        PreviousTier = previousTier;
        CurrentTier = currentTier;
        Snapshot = snapshot;
        Reason = reason;
    }
}

public enum EntitlementChangeReason
{
    InitialLoad = 0,
    Purchase = 1,
    Restore = 2,
    Lapse = 3,
    Promo = 4,
    DebugForce = 5
}
