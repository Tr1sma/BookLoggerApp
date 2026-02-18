using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Tests.TestHelpers;

/// <summary>
/// Mock implementation of INotificationService for testing purposes.
/// Tracks scheduled and sent notifications for assertion in tests.
/// </summary>
public class MockNotificationService : INotificationService
{
    public bool NotificationsEnabled { get; set; } = true;

    public List<(string Title, string Message)> SentNotifications { get; } = new();
    public TimeSpan? ScheduledReminderTime { get; private set; }
    public bool ReminderCancelled { get; private set; }

    public Task ScheduleReadingReminderAsync(TimeSpan time, CancellationToken ct = default)
    {
        if (!NotificationsEnabled)
            return Task.CompletedTask;

        ScheduledReminderTime = time;
        ReminderCancelled = false;
        return Task.CompletedTask;
    }

    public Task CancelReadingReminderAsync(CancellationToken ct = default)
    {
        ScheduledReminderTime = null;
        ReminderCancelled = true;
        return Task.CompletedTask;
    }

    public Task SendGoalCompletedNotificationAsync(string goalTitle, CancellationToken ct = default)
    {
        if (!NotificationsEnabled)
            return Task.CompletedTask;

        SentNotifications.Add(("Goal Completed!", $"Congratulations! You've completed your goal: {goalTitle}"));
        return Task.CompletedTask;
    }

    public Task SendPlantNeedsWaterNotificationAsync(string plantName, CancellationToken ct = default)
    {
        if (!NotificationsEnabled)
            return Task.CompletedTask;

        SentNotifications.Add(("Your Plant Needs Water!", $"{plantName} is thirsty! Give it some water to keep it healthy."));
        return Task.CompletedTask;
    }

    public Task SendNotificationAsync(string title, string message, CancellationToken ct = default)
    {
        SentNotifications.Add((title, message));
        return Task.CompletedTask;
    }

    public Task<bool> AreNotificationsEnabledAsync(CancellationToken ct = default)
    {
        return Task.FromResult(NotificationsEnabled);
    }
}
