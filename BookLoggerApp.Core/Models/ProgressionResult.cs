namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents the result of earning XP (from sessions or book completion).
/// </summary>
public class ProgressionResult
{
    /// <summary>
    /// Total XP earned (after boost applied).
    /// </summary>
    public int XpEarned { get; set; }

    /// <summary>
    /// Base XP earned before any boosts.
    /// </summary>
    public int BaseXp { get; set; }

    /// <summary>
    /// The plant boost percentage applied (e.g., 0.25 for 25%).
    /// </summary>
    public decimal PlantBoostPercentage { get; set; }

    /// <summary>
    /// The XP gained from plant boost (XpEarned - BaseXp).
    /// </summary>
    public int BoostedXp { get; set; }

    /// <summary>
    /// The user's new total XP after this gain.
    /// </summary>
    public int NewTotalXp { get; set; }

    /// <summary>
    /// Level-up information if a level-up occurred, otherwise null.
    /// </summary>
    public LevelUpResult? LevelUp { get; set; }
}
