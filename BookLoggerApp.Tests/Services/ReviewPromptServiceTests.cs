using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class ReviewPromptServiceTests
{
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly ReviewPromptService _service;
    private readonly AppSettings _settings;

    public ReviewPromptServiceTests()
    {
        _settingsProvider = Substitute.For<IAppSettingsProvider>();
        _settings = new AppSettings
        {
            Id = Guid.NewGuid(),
            UserLevel = 7
        };

        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(_settings));

        _service = new ReviewPromptService(_settingsProvider);
    }

    [Fact]
    public async Task TryStartPromptAsync_ShouldReturnFalse_WhenUserLevelIsSixOrLower()
    {
        // Arrange
        _settings.UserLevel = 6;

        // Act
        var result = await _service.TryStartPromptAsync();

        // Assert
        result.Should().BeFalse();
        await _settingsProvider.DidNotReceive().UpdateSettingsAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryStartPromptAsync_ShouldReturnFalse_WhenPromptWasDisabled()
    {
        // Arrange
        _settings.ReviewPromptDisabled = true;

        // Act
        var result = await _service.TryStartPromptAsync();

        // Assert
        result.Should().BeFalse();
        await _settingsProvider.DidNotReceive().UpdateSettingsAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryStartPromptAsync_ShouldReturnFalse_WhenMonthlyLimitWasReached()
    {
        // Arrange
        _settings.LastReviewPromptDate = DateTime.UtcNow;
        _settings.ReviewPromptMonthCount = 2;

        // Act
        var result = await _service.TryStartPromptAsync();

        // Assert
        result.Should().BeFalse();
        await _settingsProvider.DidNotReceive().UpdateSettingsAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryStartPromptAsync_ShouldResetMonthlyCounter_WhenLastPromptWasInPreviousMonth()
    {
        // Arrange
        _settings.LastReviewPromptDate = DateTime.UtcNow.AddMonths(-1);
        _settings.ReviewPromptMonthCount = 2;

        // Act
        var result = await _service.TryStartPromptAsync();

        // Assert
        result.Should().BeTrue();
        _settings.ReviewPromptMonthCount.Should().Be(1);
        _settings.LastReviewPromptDate.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
        await _settingsProvider.Received(1).UpdateSettingsAsync(_settings, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisablePromptAsync_ShouldPersistDisabledFlag()
    {
        // Act
        await _service.DisablePromptAsync();

        // Assert
        _settings.ReviewPromptDisabled.Should().BeTrue();
        await _settingsProvider.Received(1).UpdateSettingsAsync(_settings, Arg.Any<CancellationToken>());
    }
}