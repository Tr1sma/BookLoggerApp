namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Persists reading timer state across app background/resume cycles.
/// Ensures the timer survives screen-off events and process termination.
/// </summary>
public interface ITimerStateService
{
    /// <summary>
    /// Saves the current timer state to persistent storage.
    /// </summary>
    void SaveState(TimerStateData state);

    /// <summary>
    /// Loads the persisted timer state, if any.
    /// </summary>
    TimerStateData? LoadState();

    /// <summary>
    /// Clears the persisted timer state (call on session save or cancel).
    /// </summary>
    void ClearState();

    /// <summary>
    /// Raised when the app resumes from background.
    /// Timer components should restart their timers in response.
    /// </summary>
    event Action? AppResumed;

    /// <summary>
    /// Called by lifecycle hooks to notify that the app has resumed.
    /// </summary>
    void NotifyAppResumed();
}

/// <summary>
/// Holds timer state data for persistence across app lifecycle events.
/// </summary>
public class TimerStateData
{
    public Guid SessionId { get; set; }
    public Guid BookId { get; set; }
    public long StartTimeTicks { get; set; }
    public bool IsRunning { get; set; }
    public long PausedElapsedTicks { get; set; }

    public DateTime StartTimeUtc => new(StartTimeTicks, DateTimeKind.Utc);
    public TimeSpan PausedElapsed => TimeSpan.FromTicks(PausedElapsedTicks);
}
