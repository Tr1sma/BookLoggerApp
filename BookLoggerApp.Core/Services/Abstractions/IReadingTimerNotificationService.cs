namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Drives the platform "live reading timer" notification (Android: an ongoing
/// foreground-service notification shown on the lock screen / status bar while a
/// reading session is active). No-op on platforms without a native implementation.
/// </summary>
public interface IReadingTimerNotificationService
{
    /// <summary>
    /// Shows or updates the notification in its running state (live-ticking timer).
    /// </summary>
    void ShowRunning(ReadingTimerNotificationData data);

    /// <summary>
    /// Shows or updates the notification in its paused state (frozen elapsed time).
    /// </summary>
    void ShowPaused(ReadingTimerNotificationData data);

    /// <summary>
    /// Removes the notification (call on session save or cancel).
    /// </summary>
    void Hide();
}

/// <summary>
/// Display payload for the reading timer notification. Carries the book title so the
/// native layer doesn't need a background DB lookup on the timer hot path.
/// </summary>
public sealed record ReadingTimerNotificationData(
    Guid SessionId,
    Guid BookId,
    string BookTitle,
    DateTime StartTimeUtc,
    TimeSpan Elapsed,
    bool IsRunning,
    string? CoverImagePath = null);

/// <summary>
/// Localized strings for the notification, resolved in the app's active (in-app) language
/// and passed down to the native layer so the lock-screen text matches the language the
/// user picked in the app — not just the device locale.
/// </summary>
public sealed record ReadingTimerNotificationLabels(
    string Reading,
    string Paused,
    string Pause,
    string Resume,
    string Stop);
