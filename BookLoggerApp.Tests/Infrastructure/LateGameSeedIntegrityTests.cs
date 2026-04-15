using System.Linq;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data.SeedData;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Infrastructure;

/// <summary>
/// Guards the seed data for late-game items (Chronikbaum, Phönix, Herz der Geschichten).
/// A drift between seed data and ability logic would silently break these mechanics.
/// </summary>
public class LateGameSeedIntegrityTests
{
    [Fact]
    public void PlantSeed_ContainsChronicleTree_WithStreakGuardianAbility()
    {
        var chronicle = PlantSeedData.GetPlants()
            .FirstOrDefault(p => p.SpecialAbilityKey == SpecialAbilityKeys.StreakGuardian);

        chronicle.Should().NotBeNull("the Chronikbaum must be seeded with streak_guardian");
        chronicle!.Name.Should().Be("Chronikbaum");
        chronicle.UnlockLevel.Should().Be(45);
        chronicle.BaseCost.Should().Be(20000);
        chronicle.MaxLevel.Should().Be(40);
    }

    [Fact]
    public void PlantSeed_ContainsEternalPhoenix_WithPhoenixAbility()
    {
        var phoenix = PlantSeedData.GetPlants()
            .FirstOrDefault(p => p.SpecialAbilityKey == SpecialAbilityKeys.EternalPhoenix);

        phoenix.Should().NotBeNull("the Ewiger Phönix-Bonsai must be seeded with eternal_phoenix");
        phoenix!.Name.Should().Be("Ewiger Phönix-Bonsai");
        phoenix.UnlockLevel.Should().Be(57);
        phoenix.BaseCost.Should().Be(80000);
        phoenix.MaxLevel.Should().Be(50);
    }

    [Fact]
    public void DecorationSeed_ContainsStoryHeart_IsSingleton()
    {
        var heart = DecorationSeedData.GetDecorations()
            .FirstOrDefault(d => d.SpecialAbilityKey == SpecialAbilityKeys.StoryHeart);

        heart.Should().NotBeNull("Herz der Geschichten must be seeded with story_heart");
        heart!.Name.Should().Be("Herz der Geschichten");
        heart.UnlockLevel.Should().Be(70);
        heart.Cost.Should().Be(200000);
        heart.IsSingleton.Should().BeTrue("Herz der Geschichten must be a singleton purchase");
        heart.ItemType.Should().Be(ShopItemType.Decoration);
    }

    [Fact]
    public void PlantSeed_AllAbilityKeysReferenceKnownConstants()
    {
        var knownKeys = new[]
        {
            SpecialAbilityKeys.StreakGuardian,
            SpecialAbilityKeys.EternalPhoenix,
            SpecialAbilityKeys.StoryHeart
        };

        var unknownPlantAbilities = PlantSeedData.GetPlants()
            .Where(p => !string.IsNullOrEmpty(p.SpecialAbilityKey))
            .Where(p => !knownKeys.Contains(p.SpecialAbilityKey))
            .Select(p => p.SpecialAbilityKey)
            .ToList();

        unknownPlantAbilities.Should().BeEmpty(
            "every SpecialAbilityKey in the plant seed must correspond to a constant in SpecialAbilityKeys");
    }

    [Fact]
    public void DecorationSeed_AllAbilityKeysReferenceKnownConstants()
    {
        var knownKeys = new[]
        {
            SpecialAbilityKeys.StreakGuardian,
            SpecialAbilityKeys.EternalPhoenix,
            SpecialAbilityKeys.StoryHeart
        };

        var unknownDecorationAbilities = DecorationSeedData.GetDecorations()
            .Where(d => !string.IsNullOrEmpty(d.SpecialAbilityKey))
            .Where(d => !knownKeys.Contains(d.SpecialAbilityKey))
            .Select(d => d.SpecialAbilityKey)
            .ToList();

        unknownDecorationAbilities.Should().BeEmpty(
            "every SpecialAbilityKey in the decoration seed must correspond to a constant in SpecialAbilityKeys");
    }
}
