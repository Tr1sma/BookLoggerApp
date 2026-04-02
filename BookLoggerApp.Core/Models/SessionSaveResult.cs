namespace BookLoggerApp.Core.Models;

/// <summary>
/// Result returned when saving a reading session, including progression and goal completion state.
/// </summary>
public class SessionSaveResult
{
    public ReadingSession Session { get; set; } = null!;
    public ProgressionResult ProgressionResult { get; set; } = null!;
    public bool GoalCompleted { get; set; }
}
