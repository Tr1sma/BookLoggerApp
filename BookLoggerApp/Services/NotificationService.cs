using BookLoggerApp.Core.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;

namespace BookLoggerApp.Services;

/// <summary>
/// Service for managing local notifications using Plugin.LocalNotification.
/// </summary>
public class NotificationService : Core.Services.Abstractions.INotificationService
{
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly ILogger<NotificationService>? _logger;

    private const int ReadingReminderId = 1000;
    private const int GoalCompletedId = 2000;
    private const int PlantWaterId = 3000;

    private static int _nextNotificationId = 5000;

    private const string ReminderChannelId = "bookheart_reminders";
    private const string GeneralChannelId = "bookheart_general";

    public NotificationService(IAppSettingsProvider settingsProvider, ILogger<NotificationService>? logger = null)
    {
        _settingsProvider = settingsProvider;
        _logger = logger;
    }

    public async Task<bool> RequestNotificationPermissionAsync()
    {
        try
        {
            var result = await LocalNotificationCenter.Current.RequestNotificationPermission();
            _logger?.LogInformation("Notification permission request result: {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to request notification permission");
            return false;
        }
    }

    public async Task ScheduleReadingReminderAsync(TimeSpan time, CancellationToken ct = default)
    {
        bool enabled = await AreNotificationsEnabledAsync(ct);
        if (!enabled)
        {
            _logger?.LogInformation("Notifications are disabled, skipping reminder schedule");
            return;
        }

        // Verify OS-level permission is still granted (user may have revoked it)
        bool hasPermission = await RequestNotificationPermissionAsync();
        if (!hasPermission)
        {
            _logger?.LogWarning("OS notification permission not granted, skipping reminder schedule");
            return;
        }

        try
        {
            _logger?.LogInformation("Scheduling daily reading reminder at {Time}", time);

            // Cancel any existing reminder first
            LocalNotificationCenter.Current.Cancel(ReadingReminderId);

            DateTime notifyTime = DateTime.Today.Add(time);
            if (notifyTime <= DateTime.Now)
            {
                notifyTime = notifyTime.AddDays(1);
            }

            var notification = new NotificationRequest
            {
                NotificationId = ReadingReminderId,
                Title = "Time to Read!",
                Description = "Don't forget your daily reading session",
                CategoryType = NotificationCategoryType.Reminder,
                Android = new Plugin.LocalNotification.AndroidOption.AndroidOptions
                {
                    ChannelId = ReminderChannelId,
                },
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = notifyTime,
                    RepeatType = NotificationRepeat.Daily
                }
            };

            await LocalNotificationCenter.Current.Show(notification);
            _logger?.LogInformation("Reading reminder scheduled for {Time}", notifyTime);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to schedule reading reminder");
            throw;
        }
    }

    public Task CancelReadingReminderAsync(CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Cancelling reading reminder");
            LocalNotificationCenter.Current.Cancel(ReadingReminderId);
            _logger?.LogInformation("Reading reminder cancelled");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to cancel reading reminder");
            throw;
        }
    }

    public async Task SendGoalCompletedNotificationAsync(string goalTitle, CancellationToken ct = default)
    {
        bool enabled = await AreNotificationsEnabledAsync(ct);
        if (!enabled)
            return;

        try
        {
            _logger?.LogInformation("Sending goal completed notification for: {GoalTitle}", goalTitle);

            var notification = new NotificationRequest
            {
                NotificationId = GoalCompletedId,
                Title = "Goal Completed!",
                Description = $"Congratulations! You've completed your goal: {goalTitle}",
                Android = new Plugin.LocalNotification.AndroidOption.AndroidOptions
                {
                    ChannelId = GeneralChannelId,
                }
            };

            await LocalNotificationCenter.Current.Show(notification);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send goal completed notification");
        }
    }

    public async Task SendPlantNeedsWaterNotificationAsync(string plantName, CancellationToken ct = default)
    {
        bool enabled = await AreNotificationsEnabledAsync(ct);
        if (!enabled)
            return;

        try
        {
            _logger?.LogInformation("Sending plant water notification for: {PlantName}", plantName);

            var notification = new NotificationRequest
            {
                NotificationId = PlantWaterId,
                Title = "Your Plant Needs Water!",
                Description = $"{plantName} is thirsty! Give it some water to keep it healthy.",
                Android = new Plugin.LocalNotification.AndroidOption.AndroidOptions
                {
                    ChannelId = GeneralChannelId,
                }
            };

            await LocalNotificationCenter.Current.Show(notification);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send plant water notification");
        }
    }

    public async Task SendNotificationAsync(string title, string message, CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Sending notification: {Title}", title);

            var notification = new NotificationRequest
            {
                NotificationId = Interlocked.Increment(ref _nextNotificationId),
                Title = title,
                Description = message,
                Android = new Plugin.LocalNotification.AndroidOption.AndroidOptions
                {
                    ChannelId = GeneralChannelId,
                }
            };

            await LocalNotificationCenter.Current.Show(notification);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send notification");
            throw;
        }
    }

    public async Task<bool> AreNotificationsEnabledAsync(CancellationToken ct = default)
    {
        try
        {
            var settings = await _settingsProvider.GetSettingsAsync(ct);
            return settings.NotificationsEnabled;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to check notification settings");
            return false;
        }
    }
}
