using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Infrastructure.Services.Helpers;

public static class PlantGrowthCalculator
{
    /// <summary>Formula: 100 * 1.5^(level-1) / growthRate</summary>
    public static int GetXpForLevel(int level, double growthRate = 1.0)
    {
        if (level <= 1)
            return 0;

        var baseXp = (int)(100 * Math.Pow(1.5, level - 1));
        return (int)(baseXp / growthRate);
    }

    public static int GetTotalXpForLevel(int level, double growthRate = 1.0)
    {
        if (level <= 1)
            return 0;

        int totalXp = 0;
        for (int i = 2; i <= level; i++)
        {
            totalXp += GetXpForLevel(i, growthRate);
        }
        return totalXp;
    }

    public static int CalculateLevelFromXp(int totalXp, double growthRate = 1.0, int maxLevel = 100)
    {
        int level = 1;
        int xpAccumulated = 0;

        while (level < maxLevel)
        {
            int xpForNextLevel = GetXpForLevel(level + 1, growthRate);
            if (xpAccumulated + xpForNextLevel > totalXp)
                break;

            xpAccumulated += xpForNextLevel;
            level++;
        }

        return level;
    }

    public static int GetXpToNextLevel(int currentLevel, int currentXp, double growthRate = 1.0, int maxLevel = 100)
    {
        if (currentLevel >= maxLevel)
            return 0;

        int totalXpForCurrentLevel = GetTotalXpForLevel(currentLevel, growthRate);
        int totalXpForNextLevel = GetTotalXpForLevel(currentLevel + 1, growthRate);
        int xpIntoCurrentLevel = currentXp - totalXpForCurrentLevel;

        return (totalXpForNextLevel - totalXpForCurrentLevel) - xpIntoCurrentLevel;
    }

    public static int GetXpPercentage(int currentLevel, int currentXp, double growthRate = 1.0)
    {
        int totalXpForCurrent = GetTotalXpForLevel(currentLevel, growthRate);
        int totalXpForNext = GetTotalXpForLevel(currentLevel + 1, growthRate);
        int xpIntoCurrentLevel = currentXp - totalXpForCurrent;
        int xpNeededForLevel = totalXpForNext - totalXpForCurrent;

        if (xpNeededForLevel == 0)
            return 100;

        return Math.Clamp((xpIntoCurrentLevel * 100) / xpNeededForLevel, 0, 100);
    }

    /// <summary>
    /// Plants die after 2 missed watering intervals.
    /// Higher <paramref name="globalGrowthMultiplier"/> shortens the effective interval (faster thirst).
    /// </summary>
    public static PlantStatus CalculatePlantStatus(DateTime lastWatered, int waterIntervalDays, double globalGrowthMultiplier = 1.0)
    {
        var daysSinceWatered = (DateTime.UtcNow - lastWatered).TotalDays;
        double effectiveInterval = globalGrowthMultiplier > 0
            ? waterIntervalDays / globalGrowthMultiplier
            : waterIntervalDays;

        if (daysSinceWatered < effectiveInterval)
            return PlantStatus.Healthy;
        else if (daysSinceWatered < effectiveInterval * 1.5)
            return PlantStatus.Thirsty;
        else if (daysSinceWatered < effectiveInterval * 2)
            return PlantStatus.Wilting;
        else
            return PlantStatus.Dead;
    }

    /// <summary>
    /// Returns true within 6 hours of becoming thirsty.
    /// <paramref name="globalGrowthMultiplier"/> must mirror <see cref="CalculatePlantStatus"/>.
    /// </summary>
    public static bool NeedsWateringSoon(DateTime lastWatered, int waterIntervalDays, double globalGrowthMultiplier = 1.0)
    {
        var hoursSinceWatered = (DateTime.UtcNow - lastWatered).TotalHours;
        double effectiveIntervalDays = globalGrowthMultiplier > 0
            ? waterIntervalDays / globalGrowthMultiplier
            : waterIntervalDays;
        var hoursUntilThirsty = effectiveIntervalDays * 24.0;

        return hoursSinceWatered >= (hoursUntilThirsty - 6);
    }

    /// <summary>Multiplier semantics mirror <see cref="CalculatePlantStatus"/>.</summary>
    public static double GetDaysUntilWaterNeeded(DateTime lastWatered, int waterIntervalDays, double globalGrowthMultiplier = 1.0)
    {
        var daysSinceWatered = (DateTime.UtcNow - lastWatered).TotalDays;
        double effectiveIntervalDays = globalGrowthMultiplier > 0
            ? waterIntervalDays / globalGrowthMultiplier
            : waterIntervalDays;
        return Math.Max(0, effectiveIntervalDays - daysSinceWatered);
    }

    /// <summary>Multiplier semantics mirror <see cref="NeedsWateringSoon"/>.</summary>
    public static DateTime GetNextWaterDueAt(DateTime lastWatered, int waterIntervalDays, double globalGrowthMultiplier = 1.0)
    {
        double effectiveIntervalDays = globalGrowthMultiplier > 0
            ? waterIntervalDays / globalGrowthMultiplier
            : waterIntervalDays;
        return lastWatered.AddDays(effectiveIntervalDays);
    }

    public static bool CanLevelUp(int currentLevel, int currentXp, double growthRate, int maxLevel)
    {
        if (currentLevel >= maxLevel)
            return false;

        int totalXpForNextLevel = GetTotalXpForLevel(currentLevel + 1, growthRate);
        return currentXp >= totalXpForNextLevel;
    }

    #region Reading Days Based Leveling

    /// <summary>
    /// Formula: floor(readingDays * growthRate * globalGrowthMultiplier / 3) + 1.
    /// Herz der Geschichten passes globalGrowthMultiplier = 2.0 to halve required days.
    /// </summary>
    public static int CalculateLevelFromReadingDays(int readingDays, double growthRate, int maxLevel, double globalGrowthMultiplier = 1.0)
    {
        if (readingDays <= 0)
            return 1;

        int level = (int)Math.Floor(readingDays * growthRate * globalGrowthMultiplier / 3.0) + 1;
        return Math.Min(level, maxLevel);
    }

    /// <summary>
    /// Formula: ceil((level - 1) * 3 / (growthRate * globalGrowthMultiplier)).
    /// Must mirror <see cref="CalculateLevelFromReadingDays"/> to keep display helpers in sync.
    /// </summary>
    public static int GetReadingDaysForLevel(int level, double growthRate, double globalGrowthMultiplier = 1.0)
    {
        if (level <= 1)
            return 0;

        double effective = growthRate * globalGrowthMultiplier;
        if (effective <= 0)
            return int.MaxValue;

        return (int)Math.Ceiling((level - 1) * 3.0 / effective);
    }

    public static int GetReadingDaysToNextLevel(int currentLevel, int readingDays, double growthRate, int maxLevel, double globalGrowthMultiplier = 1.0)
    {
        if (currentLevel >= maxLevel)
            return 0;

        int daysForNextLevel = GetReadingDaysForLevel(currentLevel + 1, growthRate, globalGrowthMultiplier);
        return Math.Max(0, daysForNextLevel - readingDays);
    }

    public static int GetReadingDaysPercentage(int currentLevel, int readingDays, double growthRate, int maxLevel, double globalGrowthMultiplier = 1.0)
    {
        if (currentLevel >= maxLevel)
            return 100;

        int daysForCurrent = GetReadingDaysForLevel(currentLevel, growthRate, globalGrowthMultiplier);
        int daysForNext = GetReadingDaysForLevel(currentLevel + 1, growthRate, globalGrowthMultiplier);
        int daysIntoLevel = readingDays - daysForCurrent;
        int daysNeeded = daysForNext - daysForCurrent;

        if (daysNeeded == 0)
            return 100;

        return Math.Clamp((daysIntoLevel * 100) / daysNeeded, 0, 100);
    }

    #endregion
}
