using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class NotificationServiceTests
{
    [Fact]
    public async Task AreNotificationsEnabledAsync_WhenSettingsExist_ShouldReturnCorrectValue()
    {
        // Arrange
        using var context = TestDbContext.Create();

        // Get existing settings (from seed data) or create new
        var settings = await context.AppSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new AppSettings();
            context.AppSettings.Add(settings);
        }

        settings.NotificationsEnabled = true;
        await context.SaveChangesAsync();

        var service = new NotificationService(context);

        // Act
        var result = await service.AreNotificationsEnabledAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AreNotificationsEnabledAsync_WhenNoSettings_ShouldReturnFalse()
    {
        // Arrange
        using var context = TestDbContext.Create();
        var service = new NotificationService(context);

        // Act
        var result = await service.AreNotificationsEnabledAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ScheduleReadingReminderAsync_WhenNotificationsDisabled_ShouldNotThrow()
    {
        // Arrange
        using var context = TestDbContext.Create();
        context.AppSettings.Add(new AppSettings
        {
            NotificationsEnabled = false
        });
        await context.SaveChangesAsync();

        var service = new NotificationService(context);

        // Act
        Func<Task> act = async () => await service.ScheduleReadingReminderAsync(TimeSpan.FromHours(20));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendGoalCompletedNotificationAsync_ShouldNotThrow()
    {
        // Arrange
        using var context = TestDbContext.Create();
        context.AppSettings.Add(new AppSettings
        {
            NotificationsEnabled = true
        });
        await context.SaveChangesAsync();

        var service = new NotificationService(context);

        // Act
        Func<Task> act = async () => await service.SendGoalCompletedNotificationAsync("Read 5 books");

        // Assert - Should not throw even though notification is a placeholder
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendPlantNeedsWaterNotificationAsync_ShouldNotThrow()
    {
        // Arrange
        using var context = TestDbContext.Create();
        context.AppSettings.Add(new AppSettings
        {
            NotificationsEnabled = true
        });
        await context.SaveChangesAsync();

        var service = new NotificationService(context);

        // Act
        Func<Task> act = async () => await service.SendPlantNeedsWaterNotificationAsync("My Fern");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendNotificationAsync_ShouldNotThrow()
    {
        // Arrange
        using var context = TestDbContext.Create();
        var service = new NotificationService(context);

        // Act
        Func<Task> act = async () => await service.SendNotificationAsync("Test Title", "Test Message");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CancelReadingReminderAsync_ShouldNotThrow()
    {
        // Arrange
        using var context = TestDbContext.Create();
        var service = new NotificationService(context);

        // Act
        Func<Task> act = async () => await service.CancelReadingReminderAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }
}
