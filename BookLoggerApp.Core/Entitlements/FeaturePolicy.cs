namespace BookLoggerApp.Core.Entitlements;

/// <summary>
/// Single source of truth mapping every <see cref="FeatureKey"/> to the minimum
/// <see cref="SubscriptionTier"/> required to use it. Consumed both by runtime
/// gating (<c>IEntitlementService.HasAccess</c>) and the paywall comparison table.
/// </summary>
public static class FeaturePolicy
{
    public static IReadOnlyDictionary<FeatureKey, SubscriptionTier> MinimumTiers { get; } =
        new Dictionary<FeatureKey, SubscriptionTier>
        {
            [FeatureKey.UnlimitedNotesAndQuotes] = SubscriptionTier.Plus,
            [FeatureKey.UnlimitedReadingGoals] = SubscriptionTier.Plus,
            [FeatureKey.ReadingGoalsWithGenreTropeFilter] = SubscriptionTier.Premium,
            [FeatureKey.UnlimitedShelves] = SubscriptionTier.Plus,
            [FeatureKey.CustomShelfColors] = SubscriptionTier.Plus,
            [FeatureKey.StandardPlantsAndDecorations] = SubscriptionTier.Plus,
            [FeatureKey.PrestigePlants] = SubscriptionTier.Premium,
            [FeatureKey.UltimateDecorations] = SubscriptionTier.Premium,
            [FeatureKey.StatsTrendsTab] = SubscriptionTier.Premium,
            [FeatureKey.StatsInsightsTab] = SubscriptionTier.Premium,
            [FeatureKey.ShareCards] = SubscriptionTier.Premium,
            [FeatureKey.Wishlist] = SubscriptionTier.Plus,
            [FeatureKey.Tropes] = SubscriptionTier.Plus,
            [FeatureKey.PremiumThemes] = SubscriptionTier.Plus,
            [FeatureKey.FeatureSuggestionForm] = SubscriptionTier.Premium,
            [FeatureKey.FamilySharing] = SubscriptionTier.Premium
        };

    public static SubscriptionTier GetMinimumTier(FeatureKey feature)
    {
        if (MinimumTiers.TryGetValue(feature, out SubscriptionTier tier))
        {
            return tier;
        }

        throw new InvalidOperationException($"No FeaturePolicy entry defined for {feature}. Add it to {nameof(MinimumTiers)} and update tests.");
    }

    public static bool IsUnlockedFor(FeatureKey feature, SubscriptionTier currentTier)
    {
        return currentTier >= GetMinimumTier(feature);
    }
}
