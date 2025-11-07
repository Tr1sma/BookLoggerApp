namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a level milestone (past, current, or future).
/// </summary>
public class LevelMilestone
{
    public int Level { get; set; }
    public int XpRequired { get; set; }
    public int CoinsReward { get; set; } // Level × 50
    public bool IsCompleted { get; set; }
    public bool IsCurrent { get; set; }
}
