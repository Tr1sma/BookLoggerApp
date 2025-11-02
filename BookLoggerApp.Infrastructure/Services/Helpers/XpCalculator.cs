namespace BookLoggerApp.Infrastructure.Services.Helpers;

/// <summary>
/// Helper class for calculating XP earned from reading sessions.
/// </summary>
public static class XpCalculator
{
    private const int XP_PER_MINUTE = 1;
    private const int XP_PER_PAGE = 2;
    private const int BONUS_XP_LONG_SESSION = 50; // 60+ minutes
    private const int BONUS_XP_STREAK = 20; // Daily streak

    public static int CalculateXpForSession(int minutes, int? pagesRead, bool hasStreak = false)
    {
        int xp = 0;

        // Base XP
        xp += minutes * XP_PER_MINUTE;

        if (pagesRead.HasValue)
        {
            xp += pagesRead.Value * XP_PER_PAGE;
        }

        // Bonus for long sessions (60+ minutes)
        if (minutes >= 60)
        {
            xp += BONUS_XP_LONG_SESSION;
        }

        // Streak bonus
        if (hasStreak)
        {
            xp += BONUS_XP_STREAK;
        }

        return xp;
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

        return level - 1; // Return completed level
    }
}
