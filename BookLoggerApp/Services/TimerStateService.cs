using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Services;

/// <summary>
/// Persists reading timer state to MAUI Preferences so the timer
/// survives app background/resume cycles and process termination.
/// </summary>
public class TimerStateService : ITimerStateService
{
    private const string PrefKeySessionId = "timer_session_id";
    private const string PrefKeyBookId = "timer_book_id";
    private const string PrefKeyStartTimeTicks = "timer_start_ticks";
    private const string PrefKeyIsRunning = "timer_is_running";
    private const string PrefKeyPausedElapsedTicks = "timer_paused_elapsed_ticks";

    public event Action? AppResumed;

    public void SaveState(TimerStateData state)
    {
        Preferences.Set(PrefKeySessionId, state.SessionId.ToString());
        Preferences.Set(PrefKeyBookId, state.BookId.ToString());
        Preferences.Set(PrefKeyStartTimeTicks, state.StartTimeTicks);
        Preferences.Set(PrefKeyIsRunning, state.IsRunning);
        Preferences.Set(PrefKeyPausedElapsedTicks, state.PausedElapsedTicks);
    }

    public TimerStateData? LoadState()
    {
        var sessionIdStr = Preferences.Get(PrefKeySessionId, string.Empty);
        if (string.IsNullOrEmpty(sessionIdStr) || !Guid.TryParse(sessionIdStr, out var sessionId))
            return null;

        var bookIdStr = Preferences.Get(PrefKeyBookId, string.Empty);
        if (!Guid.TryParse(bookIdStr, out var bookId))
            return null;

        return new TimerStateData
        {
            SessionId = sessionId,
            BookId = bookId,
            StartTimeTicks = Preferences.Get(PrefKeyStartTimeTicks, 0L),
            IsRunning = Preferences.Get(PrefKeyIsRunning, false),
            PausedElapsedTicks = Preferences.Get(PrefKeyPausedElapsedTicks, 0L)
        };
    }

    public void ClearState()
    {
        Preferences.Remove(PrefKeySessionId);
        Preferences.Remove(PrefKeyBookId);
        Preferences.Remove(PrefKeyStartTimeTicks);
        Preferences.Remove(PrefKeyIsRunning);
        Preferences.Remove(PrefKeyPausedElapsedTicks);
    }

    public void NotifyAppResumed()
    {
        AppResumed?.Invoke();
    }
}
