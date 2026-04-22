using BookLoggerApp.Core.Entitlements;

namespace BookLoggerApp.Core.Exceptions;

/// <summary>
/// Thrown when a user attempts an action gated behind a higher subscription tier
/// (e.g. creating a 4th note on Free, purchasing a Prestige plant on Plus).
/// The caller is expected to catch this and open the paywall via
/// <c>IPaywallCoordinator.ShowPaywallAsync(Feature)</c>.
/// </summary>
public class EntitlementRequiredException : BookLoggerException
{
    public EntitlementRequiredException(FeatureKey feature, SubscriptionTier requiredTier, SubscriptionTier currentTier, string? context = null)
        : base(context ?? $"{feature} requires {requiredTier}; current tier is {currentTier}.")
    {
        Feature = feature;
        RequiredTier = requiredTier;
        CurrentTier = currentTier;
    }

    public FeatureKey Feature { get; }

    public SubscriptionTier RequiredTier { get; }

    public SubscriptionTier CurrentTier { get; }
}
