namespace BookLoggerApp.Core.Models;

/// <summary>
/// Result returned when ending a reading session, containing both session data and progression details.
/// </summary>
public class SessionEndResult
{
    public ReadingSession Session { get; set; } = null!;
    public ProgressionResult ProgressionResult { get; set; } = null!;
}
