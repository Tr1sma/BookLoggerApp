using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Helpers;

public class SpecialAbilityResolverTests
{
    [Fact]
    public void AnyAlivePlantHasAbility_WithMatchingAlivePlant_ReturnsTrue()
    {
        var plants = new[]
        {
            CreatePlant(SpecialAbilityKeys.StreakGuardian, PlantStatus.Healthy)
        };

        SpecialAbilityResolver
            .AnyAlivePlantHasAbility(plants, SpecialAbilityKeys.StreakGuardian)
            .Should().BeTrue();
    }

    [Fact]
    public void AnyAlivePlantHasAbility_WithDeadPlantOnly_ReturnsFalse()
    {
        var plants = new[]
        {
            CreatePlant(SpecialAbilityKeys.EternalPhoenix, PlantStatus.Dead)
        };

        SpecialAbilityResolver
            .AnyAlivePlantHasAbility(plants, SpecialAbilityKeys.EternalPhoenix)
            .Should().BeFalse();
    }

    [Fact]
    public void AnyAlivePlantHasAbility_WithOtherAbility_ReturnsFalse()
    {
        var plants = new[]
        {
            CreatePlant(SpecialAbilityKeys.StreakGuardian, PlantStatus.Healthy)
        };

        SpecialAbilityResolver
            .AnyAlivePlantHasAbility(plants, SpecialAbilityKeys.EternalPhoenix)
            .Should().BeFalse();
    }

    [Fact]
    public void AnyAlivePlantHasAbility_WithNullSpecies_ReturnsFalse()
    {
        var plants = new[]
        {
            new UserPlant { Species = null!, Status = PlantStatus.Healthy }
        };

        SpecialAbilityResolver
            .AnyAlivePlantHasAbility(plants, SpecialAbilityKeys.StreakGuardian)
            .Should().BeFalse();
    }

    [Fact]
    public void AnyDecorationHasAbility_WithMatching_ReturnsTrue()
    {
        var decos = new[]
        {
            CreateDecoration(SpecialAbilityKeys.StoryHeart)
        };

        SpecialAbilityResolver
            .AnyDecorationHasAbility(decos, SpecialAbilityKeys.StoryHeart)
            .Should().BeTrue();
    }

    [Fact]
    public void AnyDecorationHasAbility_WithNoMatch_ReturnsFalse()
    {
        var decos = new[]
        {
            CreateDecoration(null)
        };

        SpecialAbilityResolver
            .AnyDecorationHasAbility(decos, SpecialAbilityKeys.StoryHeart)
            .Should().BeFalse();
    }

    [Fact]
    public void CanGuardianSaveStreak_NullLastSave_ReturnsTrue()
    {
        var guardian = new UserPlant { LastStreakSaveAt = null };

        SpecialAbilityResolver.CanGuardianSaveStreak(guardian, DateTime.UtcNow)
            .Should().BeTrue();
    }

    [Fact]
    public void CanGuardianSaveStreak_JustFiredYesterday_ReturnsFalse()
    {
        var now = DateTime.UtcNow;
        var guardian = new UserPlant { LastStreakSaveAt = now.AddDays(-1) };

        SpecialAbilityResolver.CanGuardianSaveStreak(guardian, now)
            .Should().BeFalse();
    }

    [Fact]
    public void CanGuardianSaveStreak_ExactlyAtCooldown_ReturnsTrue()
    {
        var now = DateTime.UtcNow;
        var guardian = new UserPlant
        {
            LastStreakSaveAt = now.AddDays(-SpecialAbilityResolver.StreakGuardianCooldownDays)
        };

        SpecialAbilityResolver.CanGuardianSaveStreak(guardian, now)
            .Should().BeTrue();
    }

    [Fact]
    public void CanGuardianSaveStreak_DayBeforeCooldown_ReturnsFalse()
    {
        var now = DateTime.UtcNow;
        var guardian = new UserPlant
        {
            LastStreakSaveAt = now.AddDays(-(SpecialAbilityResolver.StreakGuardianCooldownDays - 1))
        };

        SpecialAbilityResolver.CanGuardianSaveStreak(guardian, now)
            .Should().BeFalse();
    }

    private static UserPlant CreatePlant(string abilityKey, PlantStatus status)
    {
        return new UserPlant
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Status = status,
            Species = new PlantSpecies
            {
                Name = "Test Species",
                MaxLevel = 10,
                XpBoostPercentage = 0.1m,
                WaterIntervalDays = 3,
                GrowthRate = 1.0,
                SpecialAbilityKey = abilityKey
            }
        };
    }

    private static UserDecoration CreateDecoration(string? abilityKey)
    {
        return new UserDecoration
        {
            Id = Guid.NewGuid(),
            Name = "Test Decoration",
            ShopItem = new ShopItem
            {
                Name = "Test",
                ItemType = ShopItemType.Decoration,
                Cost = 100,
                SpecialAbilityKey = abilityKey
            }
        };
    }
}
