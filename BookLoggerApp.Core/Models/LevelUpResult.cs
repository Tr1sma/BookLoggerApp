namespace BookLoggerApp.Core.Models;

/// <summary>Result of a level-up event.</summary>
public class LevelUpResult
{
    /// <summary>Previous level before level-up.</summary>
    public int OldLevel { get; set; }

    /// <summary>New level after level-up.</summary>
    public int NewLevel { get; set; }

    /// <summary>Total coins awarded across all levels gained (cumulative).</summary>
    public int CoinsAwarded { get; set; }

    /// <summary>New total coin balance after the award.</summary>
    public int NewTotalCoins { get; set; }
}
