using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class ProgressionServiceTests
{
    [Fact]
    public async Task AwardSessionXpAsync_WithHigherLevelPlant_ShouldAwardMoreXp()
    {
        // Arrange
        var lowLevelResult = await AwardSessionXpAsyncForPlantLevel(1);
        var highLevelResult = await AwardSessionXpAsyncForPlantLevel(5);

        // Assert
        lowLevelResult.PlantBoostPercentage.Should().Be(0.055m);
        highLevelResult.PlantBoostPercentage.Should().Be(0.075m);
        lowLevelResult.XpEarned.Should().Be(84);
        highLevelResult.XpEarned.Should().Be(86);
        highLevelResult.XpEarned.Should().BeGreaterThan(lowLevelResult.XpEarned);
        highLevelResult.BoostedXp.Should().BeGreaterThan(lowLevelResult.BoostedXp);
    }

    [Fact]
    public async Task GetTotalPlantBoostAsync_DeadPlants_ShouldBeIgnored()
    {
        // Arrange
        var settingsProvider = CreateSettingsProvider();
        var plantService = Substitute.For<IPlantService>();
        plantService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<IReadOnlyList<UserPlant>>(
                new[]
                {
                    CreatePlant(level: 5, PlantStatus.Healthy),
                    CreatePlant(level: 10, PlantStatus.Dead)
                }));

        var service = new ProgressionService(settingsProvider, plantService);

        // Act
        var totalBoost = await service.GetTotalPlantBoostAsync();

        // Assert
        totalBoost.Should().Be(0.075m);
    }

    private static async Task<ProgressionResult> AwardSessionXpAsyncForPlantLevel(int plantLevel)
    {
        var settingsProvider = CreateSettingsProvider();
        var plantService = Substitute.For<IPlantService>();
        plantService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<IReadOnlyList<UserPlant>>(
                new[] { CreatePlant(plantLevel, PlantStatus.Healthy) }));

        var service = new ProgressionService(settingsProvider, plantService);

        return await service.AwardSessionXpAsync(16, 0, Guid.NewGuid());
    }

    private static IAppSettingsProvider CreateSettingsProvider()
    {
        var settingsProvider = Substitute.For<IAppSettingsProvider>();
        var settings = new AppSettings
        {
            Id = Guid.NewGuid(),
            UserLevel = 1,
            TotalXp = 0,
            Coins = 100
        };

        settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(settings));
        settingsProvider.UpdateSettingsAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return settingsProvider;
    }

    private static UserPlant CreatePlant(int level, PlantStatus status)
    {
        return new UserPlant
        {
            Id = Guid.NewGuid(),
            Name = $"Plant L{level}",
            CurrentLevel = level,
            Status = status,
            Species = new PlantSpecies
            {
                Name = "Test Species",
                MaxLevel = 10,
                XpBoostPercentage = 0.05m,
                WaterIntervalDays = 3,
                GrowthRate = 1.0
            }
        };
    }
}
