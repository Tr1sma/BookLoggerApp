namespace BookLoggerApp.Core.Entitlements;

/// <summary>
/// Subscription tier a user currently holds.
/// Values are monotonically increasing so that <c>currentTier &gt;= FeaturePolicy.GetMinimumTier(feature)</c>
/// is a valid entitlement check.
/// </summary>
public enum SubscriptionTier
{
    Free = 0,
    Plus = 1,
    Premium = 2
}
