namespace BookLoggerApp.Core.Models;

/// <summary>
/// Result returned when saving a reading session, including progression and goal completion state.
/// </summary>
public class SessionSaveResult
{
    public ReadingSession Session { get; set; } = null!;
    public ProgressionResult ProgressionResult { get; set; } = null!;
    public bool GoalCompleted { get; set; }

    /// <summary>True if the Chronikbaum's Streak-Wächter prevented a streak break on this save.</summary>
    public bool StreakRescuedByGuardian { get; set; }

    /// <summary>Flat coin bonus granted by Herz der Geschichten for sessions ≥ 30 min.</summary>
    public int StoryHeartCoinBonus { get; set; }

    /// <summary>Flat XP bonus granted by Herz der Geschichten for the first session of the day.</summary>
    public int StoryHeartFirstOfDayBonusXp { get; set; }
}
