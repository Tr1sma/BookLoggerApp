using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class ReviewServiceTests
{
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly IReviewPlatformLauncher _platformLauncher;
    private readonly ILogger<ReviewService> _logger;
    private readonly ReviewService _service;

    public ReviewServiceTests()
    {
        _settingsProvider = Substitute.For<IAppSettingsProvider>();
        _platformLauncher = Substitute.For<IReviewPlatformLauncher>();
        _logger = Substitute.For<ILogger<ReviewService>>();
        _service = new ReviewService(_settingsProvider, _platformLauncher, _logger);
    }

    [Fact]
    public async Task TryRequestReviewAsync_ShouldSkipBelowLevelSix()
    {
        // Arrange
        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(new AppSettings
        {
            UserLevel = 5
        });

        // Act
        await _service.TryRequestReviewAsync();

        // Assert
        await _platformLauncher.DidNotReceive().TryLaunchAsync(Arg.Any<CancellationToken>());
        await _settingsProvider.DidNotReceive().UpdateSettingsAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryRequestReviewAsync_ShouldUpdateSettingsAfterSuccessfulLaunch()
    {
        // Arrange
        var settings = new AppSettings
        {
            UserLevel = 6,
            ReviewPromptMonthCount = 0
        };

        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);
        _platformLauncher.TryLaunchAsync(Arg.Any<CancellationToken>())
            .Returns(ReviewLaunchOutcome.RequestedFromPlayStore);

        // Act
        await _service.TryRequestReviewAsync();

        // Assert
        await _settingsProvider.Received(1).UpdateSettingsAsync(settings, Arg.Any<CancellationToken>());
        Assert.NotNull(settings.LastReviewPromptDate);
        Assert.Equal(1, settings.ReviewPromptMonthCount);
    }

    [Fact]
    public async Task TryRequestReviewAsync_ShouldBlockThirdPromptInSameMonth()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(new AppSettings
        {
            UserLevel = 6,
            LastReviewPromptDate = now,
            ReviewPromptMonthCount = 2
        });

        // Act
        await _service.TryRequestReviewAsync();

        // Assert
        await _platformLauncher.DidNotReceive().TryLaunchAsync(Arg.Any<CancellationToken>());
        await _settingsProvider.DidNotReceive().UpdateSettingsAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryRequestReviewAsync_ShouldResetMonthlyCountAfterMonthChange()
    {
        // Arrange
        var settings = new AppSettings
        {
            UserLevel = 6,
            LastReviewPromptDate = DateTime.UtcNow.AddMonths(-1),
            ReviewPromptMonthCount = 2
        };

        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);
        _platformLauncher.TryLaunchAsync(Arg.Any<CancellationToken>())
            .Returns(ReviewLaunchOutcome.RequestedFromPlayStore);

        // Act
        await _service.TryRequestReviewAsync();

        // Assert
        await _settingsProvider.Received(1).UpdateSettingsAsync(settings, Arg.Any<CancellationToken>());
        Assert.Equal(1, settings.ReviewPromptMonthCount);
    }

    [Fact]
    public async Task TryRequestReviewAsync_ShouldNotUpdateSettingsWhenLauncherDoesNotReachPlayStore()
    {
        // Arrange
        var originalLastPrompt = DateTime.UtcNow.AddDays(-2);
        var settings = new AppSettings
        {
            UserLevel = 6,
            LastReviewPromptDate = originalLastPrompt,
            ReviewPromptMonthCount = 1
        };

        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);
        _platformLauncher.TryLaunchAsync(Arg.Any<CancellationToken>())
            .Returns(ReviewLaunchOutcome.SkippedNoActivity);

        // Act
        await _service.TryRequestReviewAsync();

        // Assert
        await _settingsProvider.DidNotReceive().UpdateSettingsAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
        Assert.Equal(originalLastPrompt, settings.LastReviewPromptDate);
        Assert.Equal(1, settings.ReviewPromptMonthCount);
    }

    [Fact]
    public async Task TryRequestReviewAsync_ShouldNotEnforceFourteenDayCooldown()
    {
        // Arrange
        var settings = new AppSettings
        {
            UserLevel = 6,
            LastReviewPromptDate = DateTime.UtcNow,
            ReviewPromptMonthCount = 1
        };

        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(settings);
        _platformLauncher.TryLaunchAsync(Arg.Any<CancellationToken>())
            .Returns(ReviewLaunchOutcome.RequestedFromPlayStore);

        // Act
        await _service.TryRequestReviewAsync();

        // Assert
        await _settingsProvider.Received(1).UpdateSettingsAsync(settings, Arg.Any<CancellationToken>());
        Assert.Equal(2, settings.ReviewPromptMonthCount);
    }
}
