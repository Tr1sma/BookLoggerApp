namespace BookLoggerApp.Core.Services.Abstractions;

/// <summary>
/// Service for managing local notifications and reminders.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Schedules a daily reading reminder notification.
    /// </summary>
    /// <param name="time">The time of day to send the reminder (e.g., 20:00 for 8 PM).</param>
    /// <param name="ct">Cancellation token.</param>
    Task ScheduleReadingReminderAsync(TimeSpan time, CancellationToken ct = default);

    /// <summary>
    /// Cancels the scheduled reading reminder.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task CancelReadingReminderAsync(CancellationToken ct = default);

    /// <summary>
    /// Sends a notification when a reading goal is completed.
    /// </summary>
    /// <param name="goalTitle">The title of the completed goal.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendGoalCompletedNotificationAsync(string goalTitle, CancellationToken ct = default);

    /// <summary>
    /// Sends a notification when a plant needs watering.
    /// </summary>
    /// <param name="plantName">The name of the plant that needs water.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendPlantNeedsWaterNotificationAsync(string plantName, CancellationToken ct = default);

    /// <summary>
    /// Sends a generic notification.
    /// </summary>
    /// <param name="title">Notification title.</param>
    /// <param name="message">Notification message.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendNotificationAsync(string title, string message, CancellationToken ct = default);

    /// <summary>
    /// Checks if notifications are enabled in app settings.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if notifications are enabled, false otherwise.</returns>
    Task<bool> AreNotificationsEnabledAsync(CancellationToken ct = default);

    /// <summary>
    /// Requests the OS-level notification permission from the user.
    /// On Android 13+ this triggers the system permission dialog.
    /// </summary>
    /// <returns>True if permission was granted, false otherwise.</returns>
    Task<bool> RequestNotificationPermissionAsync();
}
