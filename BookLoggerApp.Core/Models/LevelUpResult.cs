namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents the result of a level-up event.
/// </summary>
public class LevelUpResult
{
    /// <summary>
    /// The user's previous level before level-up.
    /// </summary>
    public int OldLevel { get; set; }

    /// <summary>
    /// The user's new level after level-up.
    /// </summary>
    public int NewLevel { get; set; }

    /// <summary>
    /// Total coins awarded for all levels gained (cumulative for multiple level-ups).
    /// </summary>
    public int CoinsAwarded { get; set; }

    /// <summary>
    /// The user's new total coin balance after receiving coins.
    /// </summary>
    public int NewTotalCoins { get; set; }
}
