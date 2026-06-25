namespace BookLoggerApp.Core.Models;

/// <summary>Result of earning XP (from sessions or book completion).</summary>
public class ProgressionResult
{
    /// <summary>Total XP earned (after boost).</summary>
    public int XpEarned { get; set; }

    /// <summary>Base XP earned before boosts.</summary>
    public int BaseXp { get; set; }

    /// <summary>XP earned from reading time.</summary>
    public int MinutesXp { get; set; }

    /// <summary>XP earned from pages read.</summary>
    public int PagesXp { get; set; }

    /// <summary>XP earned from long-session bonus.</summary>
    public int LongSessionBonusXp { get; set; }

    /// <summary>XP earned from streak bonus.</summary>
    public int StreakBonusXp { get; set; }

    /// <summary>Streak length in days applied to this reward.</summary>
    public int StreakDays { get; set; }

    /// <summary>XP earned from book completion.</summary>
    public int BookCompletionXp { get; set; }

    /// <summary>Plant boost percentage applied (e.g., 0.25 = 25%).</summary>
    public decimal PlantBoostPercentage { get; set; }

    /// <summary>XP gained from plant boost (XpEarned - BaseXp).</summary>
    public int BoostedXp { get; set; }

    /// <summary>New total XP after this gain.</summary>
    public int NewTotalXp { get; set; }

    /// <summary>Level-up info if one occurred, otherwise null.</summary>
    public LevelUpResult? LevelUp { get; set; }
}
