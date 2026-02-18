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
        // Arrange
        var service = new MockNotificationService { NotificationsEnabled = true };

        // Act
        bool result = await service.AreNotificationsEnabledAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AreNotificationsEnabledAsync_WhenDisabled_ShouldReturnFalse()
    {
        // Arrange
        var service = new MockNotificationService { NotificationsEnabled = false };

        // Act
        bool result = await service.AreNotificationsEnabledAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ScheduleReadingReminderAsync_WhenNotificationsEnabled_ShouldSchedule()
    {
        // Arrange
        var service = new MockNotificationService { NotificationsEnabled = true };
        var reminderTime = TimeSpan.FromHours(20);

        // Act
        await service.ScheduleReadingReminderAsync(reminderTime);

        // Assert
        service.ScheduledReminderTime.Should().Be(reminderTime);
        service.ReminderCancelled.Should().BeFalse();
    }

    [Fact]
    public async Task ScheduleReadingReminderAsync_WhenNotificationsDisabled_ShouldNotSchedule()
    {
        // Arrange
        var service = new MockNotificationService { NotificationsEnabled = false };

        // Act
        await service.ScheduleReadingReminderAsync(TimeSpan.FromHours(20));

        // Assert
        service.ScheduledReminderTime.Should().BeNull();
    }

    [Fact]
    public async Task CancelReadingReminderAsync_ShouldCancelReminder()
    {
        // Arrange
        var service = new MockNotificationService { NotificationsEnabled = true };
        await service.ScheduleReadingReminderAsync(TimeSpan.FromHours(20));

        // Act
        await service.CancelReadingReminderAsync();

        // Assert
        service.ScheduledReminderTime.Should().BeNull();
        service.ReminderCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task SendGoalCompletedNotificationAsync_WhenEnabled_ShouldSendNotification()
    {
        // Arrange
        var service = new MockNotificationService { NotificationsEnabled = true };

        // Act
        await service.SendGoalCompletedNotificationAsync("Read 5 books");

        // Assert
        service.SentNotifications.Should().ContainSingle()
            .Which.Title.Should().Be("Goal Completed!");
    }

    [Fact]
    public async Task SendGoalCompletedNotificationAsync_WhenDisabled_ShouldNotSend()
    {
        // Arrange
        var service = new MockNotificationService { NotificationsEnabled = false };

        // Act
        await service.SendGoalCompletedNotificationAsync("Read 5 books");

        // Assert
        service.SentNotifications.Should().BeEmpty();
    }

    [Fact]
    public async Task SendPlantNeedsWaterNotificationAsync_WhenEnabled_ShouldSendNotification()
    {
        // Arrange
        var service = new MockNotificationService { NotificationsEnabled = true };

        // Act
        await service.SendPlantNeedsWaterNotificationAsync("My Fern");

        // Assert
        service.SentNotifications.Should().ContainSingle()
            .Which.Title.Should().Be("Your Plant Needs Water!");
    }

    [Fact]
    public async Task SendNotificationAsync_ShouldSendNotification()
    {
        // Arrange
        var service = new MockNotificationService();

        // Act
        await service.SendNotificationAsync("Test Title", "Test Message");

        // Assert
        service.SentNotifications.Should().ContainSingle()
            .Which.Should().Be(("Test Title", "Test Message"));
    }

    [Fact]
    public async Task INotificationService_MockInterface_ScheduleAndCancel()
    {
        // Arrange — verify NSubstitute mock works for DI scenarios
        var mock = Substitute.For<INotificationService>();
        mock.AreNotificationsEnabledAsync(Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await mock.ScheduleReadingReminderAsync(TimeSpan.FromHours(20));
        await mock.CancelReadingReminderAsync();

        // Assert
        await mock.Received(1).ScheduleReadingReminderAsync(TimeSpan.FromHours(20), Arg.Any<CancellationToken>());
        await mock.Received(1).CancelReadingReminderAsync(Arg.Any<CancellationToken>());
    }
}
