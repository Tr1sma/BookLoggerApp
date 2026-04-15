using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Helpers;

/// <summary>
/// Central lookup and constants for late-game special abilities.
/// Pure-logic helpers: no DI, no I/O. Feed in already-loaded plant / decoration lists.
/// </summary>
public static class SpecialAbilityResolver
{
    /// <summary>Minimum days between two Streak-Guardian rescues.</summary>
    public const int StreakGuardianCooldownDays = 14;

    /// <summary>XP-boost percentage (as decimal) granted by an owned Herz der Geschichten.</summary>
    public const decimal StoryHeartXpBoostPct = 0.25m;

    /// <summary>Level-up coin multiplier applied when Herz der Geschichten is owned.</summary>
    public const decimal StoryHeartCoinMultiplier = 1.25m;

    /// <summary>Flat coin bonus per qualifying reading session when Herz der Geschichten is owned.</summary>
    public const int StoryHeartSessionCoinBonus = 400;

    /// <summary>Minimum session duration (minutes) to earn the Herz coin bonus.</summary>
    public const int StoryHeartSessionMinMinutes = 30;

    /// <summary>Plant-growth multiplier applied globally while Herz der Geschichten is owned.</summary>
    public const decimal StoryHeartPlantGrowthMultiplier = 2.0m;

    /// <summary>Fraction of the next-level XP requirement granted on the first session of each day when Herz der Geschichten is owned.</summary>
    public const decimal StoryHeartFirstSessionXpPct = 0.025m;

    /// <summary>
    /// Returns true if any alive plant in the list has the given SpecialAbilityKey.
    /// A dead plant never counts, regardless of its ability.
    /// </summary>
    public static bool AnyAlivePlantHasAbility(IEnumerable<UserPlant> plants, string abilityKey)
    {
        if (plants is null || string.IsNullOrEmpty(abilityKey))
        {
            return false;
        }

        foreach (UserPlant plant in plants)
        {
            if (plant.Status == PlantStatus.Dead)
            {
                continue;
            }

            if (plant.Species?.SpecialAbilityKey == abilityKey)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true if any decoration in the list has the given SpecialAbilityKey.
    /// Decorations have no lifecycle — ownership alone activates them.
    /// </summary>
    public static bool AnyDecorationHasAbility(IEnumerable<UserDecoration> decorations, string abilityKey)
    {
        if (decorations is null || string.IsNullOrEmpty(abilityKey))
        {
            return false;
        }

        foreach (UserDecoration decoration in decorations)
        {
            if (decoration.ShopItem?.SpecialAbilityKey == abilityKey)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true if the given guardian plant is ready to rescue a streak (never fired, or cooldown elapsed).
    /// Caller is responsible for also checking the plant is alive and has the StreakGuardian ability.
    /// </summary>
    public static bool CanGuardianSaveStreak(UserPlant guardian, DateTime utcNow)
    {
        if (guardian is null)
        {
            return false;
        }

        if (guardian.LastStreakSaveAt is null)
        {
            return true;
        }

        return (utcNow - guardian.LastStreakSaveAt.Value).TotalDays >= StreakGuardianCooldownDays;
    }
}
