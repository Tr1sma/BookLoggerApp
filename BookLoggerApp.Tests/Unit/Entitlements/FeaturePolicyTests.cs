using System;
using BookLoggerApp.Core.Entitlements;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Entitlements;

public class FeaturePolicyTests
{
    [Fact]
    public void MinimumTiers_CoversEveryFeatureKey()
    {
        foreach (FeatureKey feature in Enum.GetValues<FeatureKey>())
        {
            FeaturePolicy.MinimumTiers.Should().ContainKey(feature,
                $"every FeatureKey must have a FeaturePolicy entry; missing: {feature}");
        }
    }

    [Fact]
    public void MinimumTiers_NeverMapsToFree()
    {
        foreach ((FeatureKey feature, SubscriptionTier minimum) in FeaturePolicy.MinimumTiers)
        {
            minimum.Should().NotBe(SubscriptionTier.Free,
                $"{feature} is a gated feature; Free-tier items should be controlled via seed flags, not FeaturePolicy");
        }
    }

    [Theory]
    [InlineData(FeatureKey.UnlimitedNotesAndQuotes, SubscriptionTier.Plus)]
    [InlineData(FeatureKey.Wishlist, SubscriptionTier.Plus)]
    [InlineData(FeatureKey.Tropes, SubscriptionTier.Plus)]
    [InlineData(FeatureKey.PremiumThemes, SubscriptionTier.Plus)]
    [InlineData(FeatureKey.StandardPlantsAndDecorations, SubscriptionTier.Plus)]
    [InlineData(FeatureKey.UnlimitedShelves, SubscriptionTier.Plus)]
    [InlineData(FeatureKey.CustomShelfColors, SubscriptionTier.Plus)]
    [InlineData(FeatureKey.UnlimitedReadingGoals, SubscriptionTier.Plus)]
    [InlineData(FeatureKey.StatsTrendsTab, SubscriptionTier.Premium)]
    [InlineData(FeatureKey.StatsInsightsTab, SubscriptionTier.Premium)]
    [InlineData(FeatureKey.ShareCards, SubscriptionTier.Premium)]
    [InlineData(FeatureKey.PrestigePlants, SubscriptionTier.Premium)]
    [InlineData(FeatureKey.UltimateDecorations, SubscriptionTier.Premium)]
    [InlineData(FeatureKey.ReadingGoalsWithGenreTropeFilter, SubscriptionTier.Premium)]
    [InlineData(FeatureKey.FeatureSuggestionForm, SubscriptionTier.Premium)]
    [InlineData(FeatureKey.FamilySharing, SubscriptionTier.Premium)]
    public void GetMinimumTier_MatchesProductSpec(FeatureKey feature, SubscriptionTier expected)
    {
        FeaturePolicy.GetMinimumTier(feature).Should().Be(expected);
    }

    [Theory]
    [InlineData(SubscriptionTier.Free, FeatureKey.UnlimitedNotesAndQuotes, false)]
    [InlineData(SubscriptionTier.Plus, FeatureKey.UnlimitedNotesAndQuotes, true)]
    [InlineData(SubscriptionTier.Premium, FeatureKey.UnlimitedNotesAndQuotes, true)]
    [InlineData(SubscriptionTier.Plus, FeatureKey.StatsTrendsTab, false)]
    [InlineData(SubscriptionTier.Premium, FeatureKey.StatsTrendsTab, true)]
    [InlineData(SubscriptionTier.Free, FeatureKey.PrestigePlants, false)]
    public void IsUnlockedFor_Monotonic(SubscriptionTier tier, FeatureKey feature, bool expected)
    {
        FeaturePolicy.IsUnlockedFor(feature, tier).Should().Be(expected);
    }

    [Fact]
    public void FeatureDisplay_HasEntryForEveryFeatureKey()
    {
        foreach (FeatureKey feature in Enum.GetValues<FeatureKey>())
        {
            FeatureDisplay.Info.Should().ContainKey(feature,
                $"paywall rendering requires a FeatureDisplayInfo for {feature}");
        }
    }

    [Fact]
    public void FeatureDisplay_LabelsAreEnglish()
    {
        foreach ((FeatureKey feature, FeatureDisplayInfo info) in FeatureDisplay.Info)
        {
            info.Label.Should().NotBeNullOrWhiteSpace($"{feature} needs a non-empty label");
            info.Description.Should().NotBeNullOrWhiteSpace($"{feature} needs a non-empty description");
            info.IconKey.Should().NotBeNullOrWhiteSpace($"{feature} needs a non-empty icon key");
        }
    }
}
