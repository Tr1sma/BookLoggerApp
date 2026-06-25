using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class NotificationServiceTests
{
    [Fact]
    public async Task AreNotificationsEnabledAsync_WhenEnabled_ShouldReturnTrue()
    {
        var service = new MockNotificationService { NotificationsEnabled = true };

        bool result = await service.AreNotificationsEnabledAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AreNotificationsEnabledAsync_WhenDisabled_ShouldReturnFalse()
    {
        var service = new MockNotificationService { NotificationsEnabled = false };

        bool result = await service.AreNotificationsEnabledAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ScheduleReadingReminderAsync_WhenNotificationsEnabled_ShouldSchedule()
    {
        var service = new MockNotificationService { NotificationsEnabled = true };
        var reminderTime = TimeSpan.FromHours(20);

        await service.ScheduleReadingReminderAsync(reminderTime);

        service.ScheduledReminderTime.Should().Be(reminderTime);
        service.ReminderCancelled.Should().BeFalse();
    }

    [Fact]
    public async Task ScheduleReadingReminderAsync_WhenNotificationsDisabled_ShouldNotSchedule()
    {
        var service = new MockNotificationService { NotificationsEnabled = false };

        await service.ScheduleReadingReminderAsync(TimeSpan.FromHours(20));

        service.ScheduledReminderTime.Should().BeNull();
    }

    [Fact]
    public async Task CancelReadingReminderAsync_ShouldCancelReminder()
    {
        var service = new MockNotificationService { NotificationsEnabled = true };
        await service.ScheduleReadingReminderAsync(TimeSpan.FromHours(20));

        await service.CancelReadingReminderAsync();

        service.ScheduledReminderTime.Should().BeNull();
        service.ReminderCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task SendGoalCompletedNotificationAsync_WhenEnabled_ShouldSendNotification()
    {
        var service = new MockNotificationService { NotificationsEnabled = true };

        await service.SendGoalCompletedNotificationAsync("Read 5 books");

        service.SentNotifications.Should().ContainSingle()
            .Which.Title.Should().Be("Goal Completed!");
    }

    [Fact]
    public async Task SendGoalCompletedNotificationAsync_WhenDisabled_ShouldNotSend()
    {
        var service = new MockNotificationService { NotificationsEnabled = false };

        await service.SendGoalCompletedNotificationAsync("Read 5 books");

        service.SentNotifications.Should().BeEmpty();
    }

    [Fact]
    public async Task SendPlantNeedsWaterNotificationAsync_WhenEnabled_ShouldSendNotification()
    {
        var service = new MockNotificationService { NotificationsEnabled = true };

        await service.SendPlantNeedsWaterNotificationAsync("My Fern");

        service.SentNotifications.Should().ContainSingle()
            .Which.Title.Should().Be("Your Plant Needs Water!");
    }

    [Fact]
    public async Task SendNotificationAsync_ShouldSendNotification()
    {
        var service = new MockNotificationService();

        await service.SendNotificationAsync("Test Title", "Test Message");

        service.SentNotifications.Should().ContainSingle()
            .Which.Should().Be(("Test Title", "Test Message"));
    }

    [Fact]
    public async Task INotificationService_MockInterface_ScheduleAndCancel()
    {
        // NSubstitute mock must work for DI scenarios.
        var mock = Substitute.For<INotificationService>();
        mock.AreNotificationsEnabledAsync(Arg.Any<CancellationToken>()).Returns(true);

        await mock.ScheduleReadingReminderAsync(TimeSpan.FromHours(20));
        await mock.CancelReadingReminderAsync();

        await mock.Received(1).ScheduleReadingReminderAsync(TimeSpan.FromHours(20), Arg.Any<CancellationToken>());
        await mock.Received(1).CancelReadingReminderAsync(Arg.Any<CancellationToken>());
    }
}
