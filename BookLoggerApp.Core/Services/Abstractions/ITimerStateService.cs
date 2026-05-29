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

    /// <summary>
    /// Raised when a timer command arrives from outside the in-app UI
    /// (e.g. the user tapped Pause/Resume/Stop on the lock-screen notification).
    /// The active timer component should apply it to its in-memory state.
    /// </summary>
    event Action<ExternalTimerCommand>? ExternalCommandReceived;

    /// <summary>
    /// Called by the platform notification layer to relay a timer command into the app.
    /// </summary>
    void NotifyExternalCommand(ExternalTimerCommand command);
}

/// <summary>
/// A timer command originating from outside the in-app UI (the lock-screen notification).
/// </summary>
public enum ExternalTimerCommand
{
    Pause,
    Resume,
    Stop
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
