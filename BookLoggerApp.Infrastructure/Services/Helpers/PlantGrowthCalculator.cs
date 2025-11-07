using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Infrastructure.Services.Helpers;

/// <summary>
/// Helper for calculating plant growth, XP requirements, and plant status.
/// </summary>
public static class PlantGrowthCalculator
{
    /// <summary>
    /// Calculate XP required to reach a specific level.
    /// Formula: 100 * 1.5^(level-1) / growthRate
    /// </summary>
    public static int GetXpForLevel(int level, double growthRate = 1.0)
    {
        if (level <= 1)
            return 0;

        // Base formula: 100 * 1.5^(level-1)
        var baseXp = (int)(100 * Math.Pow(1.5, level - 1));

        // Apply growth rate (higher growth rate = less XP needed)
        return (int)(baseXp / growthRate);
    }

    /// <summary>
    /// Calculate total XP needed to reach a level from level 1.
    /// </summary>
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

    /// <summary>
    /// Calculate current level based on total XP.
    /// </summary>
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

    /// <summary>
    /// Calculate XP needed for next level based on current level and XP.
    /// </summary>
    public static int GetXpToNextLevel(int currentLevel, int currentXp, double growthRate = 1.0, int maxLevel = 100)
    {
        if (currentLevel >= maxLevel)
            return 0;

        int totalXpForCurrentLevel = GetTotalXpForLevel(currentLevel, growthRate);
        int totalXpForNextLevel = GetTotalXpForLevel(currentLevel + 1, growthRate);
        int xpIntoCurrentLevel = currentXp - totalXpForCurrentLevel;

        return (totalXpForNextLevel - totalXpForCurrentLevel) - xpIntoCurrentLevel;
    }

    /// <summary>
    /// Calculate XP progress percentage for current level.
    /// </summary>
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
    /// Calculate plant status based on last watered date and water interval.
    /// Pflanzen sterben nach 2 verpassten Gießzeiten.
    /// </summary>
    public static PlantStatus CalculatePlantStatus(DateTime lastWatered, int waterIntervalDays)
    {
        var daysSinceWatered = (DateTime.UtcNow - lastWatered).TotalDays;

        // Healthy: Innerhalb des normalen Gießintervalls
        if (daysSinceWatered < waterIntervalDays)
            return PlantStatus.Healthy;

        // Thirsty: 1. verpasste Gießzeit (waterIntervalDays bis waterIntervalDays * 1.5)
        else if (daysSinceWatered < waterIntervalDays * 1.5)
            return PlantStatus.Thirsty;

        // Wilting: Kurz vor dem Tod (waterIntervalDays * 1.5 bis waterIntervalDays * 2)
        else if (daysSinceWatered < waterIntervalDays * 2)
            return PlantStatus.Wilting;

        // Dead: 2. verpasste Gießzeit (ab waterIntervalDays * 2)
        else
            return PlantStatus.Dead;
    }

    /// <summary>
    /// Check if plant needs watering soon (within 6 hours).
    /// </summary>
    public static bool NeedsWateringSoon(DateTime lastWatered, int waterIntervalDays)
    {
        var hoursSinceWatered = (DateTime.UtcNow - lastWatered).TotalHours;
        var hoursUntilThirsty = waterIntervalDays * 24;

        // Return true if within 6 hours of becoming thirsty
        return hoursSinceWatered >= (hoursUntilThirsty - 6);
    }

    /// <summary>
    /// Calculate days until plant needs water.
    /// </summary>
    public static double GetDaysUntilWaterNeeded(DateTime lastWatered, int waterIntervalDays)
    {
        var daysSinceWatered = (DateTime.UtcNow - lastWatered).TotalDays;
        return Math.Max(0, waterIntervalDays - daysSinceWatered);
    }

    /// <summary>
    /// Check if plant can level up based on current XP.
    /// </summary>
    public static bool CanLevelUp(int currentLevel, int currentXp, double growthRate, int maxLevel)
    {
        if (currentLevel >= maxLevel)
            return false;

        int totalXpForNextLevel = GetTotalXpForLevel(currentLevel + 1, growthRate);
        return currentXp >= totalXpForNextLevel;
    }
}
