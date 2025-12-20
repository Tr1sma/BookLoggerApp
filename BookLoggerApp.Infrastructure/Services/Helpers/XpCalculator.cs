namespace BookLoggerApp.Infrastructure.Services.Helpers;

/// <summary>
/// Helper class for calculating XP earned from reading sessions.
/// </summary>
public static class XpCalculator
{
    private const int XP_PER_MINUTE = 5;
    private const int XP_PER_PAGE = 20;
    private const int XP_BOOK_COMPLETION = 100; // Bonus for completing a book
    private const int BONUS_XP_LONG_SESSION = 50; // 60+ minutes
    private const int BONUS_XP_STREAK = 20; // Daily streak

    public static int CalculateXpForSession(int minutes, int? pagesRead, bool hasStreak = false)
    {
        var (minutesXp, pagesXp, longSessionXp, streakXp) = CalculateSessionXpBreakdown(minutes, pagesRead, hasStreak);
        return minutesXp + pagesXp + longSessionXp + streakXp;
    }

    public static (int MinutesXp, int PagesXp, int LongSessionXp, int StreakXp) CalculateSessionXpBreakdown(int minutes, int? pagesRead, bool hasStreak = false)
    {
        int minutesXp = minutes * XP_PER_MINUTE;
        int pagesXp = (pagesRead ?? 0) * XP_PER_PAGE;
        int longSessionXp = (minutes >= 60) ? BONUS_XP_LONG_SESSION : 0;
        int streakXp = hasStreak ? BONUS_XP_STREAK : 0;

        return (minutesXp, pagesXp, longSessionXp, streakXp);
    }

    public static int GetXpForLevel(int level)
    {
        // Exponential growth: Level 1 = 100 XP, Level 2 = 250 XP, Level 3 = 500 XP, etc.
        return (int)(100 * Math.Pow(1.5, level - 1));
    }

    public static int CalculateLevelFromXp(int totalXp)
    {
        int level = 1;
        int xpRequired = GetXpForLevel(level);

        while (totalXp >= xpRequired)
        {
            totalXp -= xpRequired;
            level++;
            xpRequired = GetXpForLevel(level);
        }

        return level; // Return current level (not level - 1!)
    }

    /// <summary>
    /// Applies plant XP boost to base XP amount.
    /// </summary>
    /// <param name="baseXp">The base XP earned before boost</param>
    /// <param name="boostPercentage">The boost percentage (e.g., 0.25 for 25% boost)</param>
    /// <returns>The boosted XP amount</returns>
    public static int ApplyPlantBoost(int baseXp, decimal boostPercentage)
    {
        return (int)(baseXp * (1 + boostPercentage));
    }

    /// <summary>
    /// Calculates XP earned for completing a book.
    /// </summary>
    /// <returns>The XP bonus for book completion</returns>
    public static int CalculateXpForBookCompletion()
    {
        return XP_BOOK_COMPLETION;
    }
}
