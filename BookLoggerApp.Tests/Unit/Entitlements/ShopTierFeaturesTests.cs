using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Models;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Entitlements;

/// <summary>
/// Pins the plant/decoration tier→FeatureKey mapping in <see cref="ShopTierFeatures"/> so the
/// shop UI and service-layer entitlement guards can never diverge.
/// </summary>
public class ShopTierFeaturesTests
{
    [Fact]
    public void For_FreeTierPlant_ReturnsNull()
    {
        var species = new PlantSpecies { IsFreeTier = true };

        ShopTierFeatures.For(species).Should().BeNull();
    }

    [Fact]
    public void For_StandardPlant_ReturnsStandardPlantsAndDecorations()
    {
        var species = new PlantSpecies { IsFreeTier = false, IsPrestigeTier = false };

        ShopTierFeatures.For(species).Should().Be(FeatureKey.StandardPlantsAndDecorations);
    }

    [Fact]
    public void For_PrestigePlant_ReturnsPrestigePlants()
    {
        var species = new PlantSpecies { IsPrestigeTier = true };

        ShopTierFeatures.For(species).Should().Be(FeatureKey.PrestigePlants);
    }

    [Fact]
    public void For_FreeTierDecoration_ReturnsNull()
    {
        var item = new ShopItem { IsFreeTier = true };

        ShopTierFeatures.For(item).Should().BeNull();
    }

    [Fact]
    public void For_StandardDecoration_ReturnsStandardPlantsAndDecorations()
    {
        var item = new ShopItem { IsFreeTier = false, IsUltimateTier = false };

        ShopTierFeatures.For(item).Should().Be(FeatureKey.StandardPlantsAndDecorations);
    }

    [Fact]
    public void For_UltimateDecoration_ReturnsUltimateDecorations()
    {
        var item = new ShopItem { IsUltimateTier = true };

        ShopTierFeatures.For(item).Should().Be(FeatureKey.UltimateDecorations);
    }
}
