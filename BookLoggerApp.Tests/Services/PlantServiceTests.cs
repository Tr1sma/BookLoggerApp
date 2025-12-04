using FluentAssertions;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class PlantServiceTests : IDisposable
{
    private readonly PlantService _plantService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DbContextTestHelper _dbHelper;
    private readonly IMemoryCache _cache;

    public PlantServiceTests()
    {
        _dbHelper = DbContextTestHelper.CreateTestContext();

        // Create memory cache for testing
        var services = new ServiceCollection();
        services.AddMemoryCache();
        var serviceProvider = services.BuildServiceProvider();
        _cache = serviceProvider.GetRequiredService<IMemoryCache>();

        _unitOfWork = new UnitOfWork(_dbHelper.Context);
        var contextFactory = new TestDbContextFactory(_dbHelper.DatabaseName);
        var settingsProvider = new AppSettingsProvider(contextFactory);
        var logger = NullLogger<PlantService>.Instance;
        _plantService = new PlantService(_unitOfWork, settingsProvider, _cache, logger);
    }

    public void Dispose()
    {
        _dbHelper.Dispose();
    }

    #region Basic CRUD Tests

    [Fact]
    public async Task AddAsync_ShouldCreatePlant()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = new UserPlant
        {
            SpeciesId = species.Id,
            Name = "Test Plant",
            CurrentLevel = 1,
            Experience = 0,
            IsActive = true
        };

        // Act
        var result = await _plantService.AddAsync(plant);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Plant");
        result.PlantedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.LastWatered.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnPlant()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "My Plant");

        // Act
        var result = await _plantService.GetByIdAsync(plant.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("My Plant");
        result.Species.Should().NotBeNull();
        result.Species.Name.Should().Be("Test Species");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllPlants()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        await SeedUserPlant(species.Id, "Plant 1");
        await SeedUserPlant(species.Id, "Plant 2");

        // Act
        var result = await _plantService.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Select(p => p.Name).Should().Contain(new[] { "Plant 1", "Plant 2" });
    }

    #endregion

    #region Active Plant Tests

    [Fact]
    public async Task SetActivePlantAsync_ShouldActivatePlantAndDeactivateOthers()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant1 = await SeedUserPlant(species.Id, "Plant 1", isActive: true);
        var plant2 = await SeedUserPlant(species.Id, "Plant 2", isActive: false);

        // Clear the change tracker to avoid tracking conflicts
        _dbHelper.Context.ChangeTracker.Clear();

        // Act
        await _plantService.SetActivePlantAsync(plant2.Id);

        // Assert
        var activePlant = await _plantService.GetActivePlantAsync();
        activePlant.Should().NotBeNull();
        activePlant!.Id.Should().Be(plant2.Id);

        var plant1Updated = await _plantService.GetByIdAsync(plant1.Id);
        plant1Updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetActivePlantAsync_WhenNoActivePlant_ShouldReturnNull()
    {
        // Act
        var result = await _plantService.GetActivePlantAsync();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Watering Tests

    [Fact]
    public async Task WaterPlantAsync_ShouldUpdateLastWateredAndStatus()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Thirsty Plant");
        
        // Set plant to thirsty by backdating last watered
        plant.LastWatered = DateTime.UtcNow.AddDays(-4);
        plant.Status = PlantStatus.Thirsty;
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        // Act
        await _plantService.WaterPlantAsync(plant.Id);

        // Assert
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant.Should().NotBeNull();
        updatedPlant!.LastWatered.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        updatedPlant.Status.Should().Be(PlantStatus.Healthy);
    }

    [Fact]
    public async Task WaterPlantAsync_WhenPlantIsDead_ShouldThrowException()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Dead Plant");
        plant.Status = PlantStatus.Dead;
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        // Act
        Func<Task> act = async () => await _plantService.WaterPlantAsync(plant.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot water a dead plant");
    }

    #endregion

    #region Experience & Leveling Tests

    [Fact]
    public async Task AddExperienceAsync_ShouldIncreaseExperience()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Growing Plant");

        // Act
        await _plantService.AddExperienceAsync(plant.Id, 50);

        // Assert
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.Experience.Should().Be(50);
    }

    [Fact]
    public async Task AddExperienceAsync_WhenEnoughForLevelUp_ShouldIncreaseLevel()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Leveling Plant");

        // Act - Add enough XP to reach level 2 (150 XP needed based on formula 100 * 1.5^1)
        await _plantService.AddExperienceAsync(plant.Id, 150);

        // Assert
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.CurrentLevel.Should().Be(2);
        updatedPlant.Experience.Should().Be(150);
    }

    [Fact]
    public async Task AddExperienceAsync_WhenEnoughForMultipleLevels_ShouldLevelUpCorrectly()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Fast Growing Plant");

        // Act - Add enough XP to reach level 3 (375 XP needed: 150 for L2 + 225 for L3)
        await _plantService.AddExperienceAsync(plant.Id, 375);

        // Assert
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.CurrentLevel.Should().Be(3);
        updatedPlant.Experience.Should().Be(375);
    }

    [Fact]
    public async Task CanLevelUpAsync_WhenEnoughXp_ShouldReturnTrue()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Ready Plant");
        plant.Experience = 150; // Enough for level 2 (150 XP needed)
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        // Act
        var canLevel = await _plantService.CanLevelUpAsync(plant.Id);

        // Assert
        canLevel.Should().BeTrue();
    }

    [Fact]
    public async Task CanLevelUpAsync_WhenNotEnoughXp_ShouldReturnFalse()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Not Ready Plant");
        plant.Experience = 50; // Not enough for level 2
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        // Act
        var canLevel = await _plantService.CanLevelUpAsync(plant.Id);

        // Assert
        canLevel.Should().BeFalse();
    }

    #endregion

    #region Purchase Tests

    [Fact]
    public async Task PurchasePlantAsync_ShouldCreateNewPlant()
    {
        // Arrange
        var species = await SeedPlantSpecies();

        // Act
        var plant = await _plantService.PurchasePlantAsync(species.Id, "Purchased Plant");

        // Assert
        plant.Should().NotBeNull();
        plant.Name.Should().Be("Purchased Plant");
        plant.SpeciesId.Should().Be(species.Id);
        plant.CurrentLevel.Should().Be(1);
        plant.Experience.Should().Be(0);
    }

    [Fact]
    public async Task PurchasePlantAsync_WhenSpeciesNotAvailable_ShouldThrowException()
    {
        // Arrange
        var species = await SeedPlantSpecies(isAvailable: false);

        // Act
        Func<Task> act = async () => await _plantService.PurchasePlantAsync(species.Id, "Test");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Plant species is not available for purchase");
    }

    #endregion

    #region Plant Status Updates

    [Fact]
    public async Task UpdatePlantStatusesAsync_ShouldUpdateStatusBasedOnLastWatered()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var healthyPlant = await SeedUserPlant(species.Id, "Healthy Plant");
        
        var thirstyPlant = await SeedUserPlant(species.Id, "Thirsty Plant");
        thirstyPlant.LastWatered = DateTime.UtcNow.AddDays(-3.5);
        await _unitOfWork.UserPlants.UpdateAsync(thirstyPlant);

        // Act
        await _plantService.UpdatePlantStatusesAsync();

        // Assert
        var healthyUpdated = await _plantService.GetByIdAsync(healthyPlant.Id);
        healthyUpdated!.Status.Should().Be(PlantStatus.Healthy);

        var thirstyUpdated = await _plantService.GetByIdAsync(thirstyPlant.Id);
        thirstyUpdated!.Status.Should().Be(PlantStatus.Thirsty);
    }

    [Fact]
    public async Task GetPlantsNeedingWaterAsync_ShouldReturnOnlyPlantsNeedingWater()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var healthyPlant = await SeedUserPlant(species.Id, "Healthy Plant");
        
        var needsWaterPlant = await SeedUserPlant(species.Id, "Needs Water");
        needsWaterPlant.LastWatered = DateTime.UtcNow.AddHours(-67); // Within 6 hours of needing water
        await _unitOfWork.UserPlants.UpdateAsync(needsWaterPlant);

        // Act
        var plantsNeedingWater = await _plantService.GetPlantsNeedingWaterAsync();

        // Assert
        plantsNeedingWater.Should().ContainSingle();
        plantsNeedingWater.First().Name.Should().Be("Needs Water");
    }

    #endregion

    #region Species Tests

    [Fact]
    public async Task GetAvailableSpeciesAsync_ShouldReturnOnlyAvailableSpeciesForUserLevel()
    {
        // Arrange - note: database may contain seeded species (Starter Sprout, Bookworm Fern)
        var species1 = await SeedPlantSpecies("Test Species 1", unlockLevel: 1, isAvailable: true);
        var species2 = await SeedPlantSpecies("Test Species 2", unlockLevel: 5, isAvailable: true);
        var species3 = await SeedPlantSpecies("Test Species 3", unlockLevel: 10, isAvailable: false);
        var species4 = await SeedPlantSpecies("Test Species 4", unlockLevel: 15, isAvailable: true);

        // Act
        var result = await _plantService.GetAvailableSpeciesAsync(userLevel: 5);

        // Assert - should include test species 1 & 2 (available and unlockLevel <= 5)
        // but not species 3 (unavailable) and species 4 (unlockLevel > 5)
        result.Select(s => s.Name).Should().Contain(new[] { "Test Species 1", "Test Species 2" });
        result.Select(s => s.Name).Should().NotContain("Test Species 3");
        result.Select(s => s.Name).Should().NotContain("Test Species 4");
        // All returned species should be available and have unlockLevel <= 5
        result.Should().OnlyContain(s => s.IsAvailable && s.UnlockLevel <= 5);
    }

    #endregion

    #region Reading Days Tests (RecordReadingDayAsync)

    [Fact]
    public async Task RecordReadingDayAsync_WithLessThan15Minutes_ShouldNotRecord()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Short Session Plant");

        // Act - 14 minutes is not enough
        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow, 14);

        // Assert
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(0);
        updatedPlant.LastReadingDayRecorded.Should().BeNull();
    }

    [Fact]
    public async Task RecordReadingDayAsync_WithExactly15Minutes_ShouldRecord()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Exact Session Plant");
        var sessionDate = DateTime.UtcNow;

        // Act - exactly 15 minutes is enough
        await _plantService.RecordReadingDayAsync(plant.Id, sessionDate, 15);

        // Assert
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(1);
        updatedPlant.LastReadingDayRecorded.Should().NotBeNull();
        updatedPlant.LastReadingDayRecorded!.Value.Date.Should().Be(sessionDate.Date);
    }

    [Fact]
    public async Task RecordReadingDayAsync_WithMoreThan15Minutes_ShouldRecord()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Long Session Plant");
        var sessionDate = DateTime.UtcNow;

        // Act - 60 minutes definitely counts
        await _plantService.RecordReadingDayAsync(plant.Id, sessionDate, 60);

        // Assert
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(1);
        updatedPlant.LastReadingDayRecorded.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordReadingDayAsync_FirstReadingDay_ShouldRecordWhenLastRecordedIsNull()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "First Day Plant");

        // Verify initial state
        var initialPlant = await _plantService.GetByIdAsync(plant.Id);
        initialPlant!.LastReadingDayRecorded.Should().BeNull();
        initialPlant.ReadingDaysCount.Should().Be(0);

        // Act
        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow, 20);

        // Assert
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(1);
        updatedPlant.LastReadingDayRecorded.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordReadingDayAsync_SameDaySecondSession_ShouldNotRecordAgain()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Same Day Plant");
        var today = DateTime.UtcNow.Date;
        var morningSession = today.AddHours(8);
        var eveningSession = today.AddHours(20);

        // Act - First session
        await _plantService.RecordReadingDayAsync(plant.Id, morningSession, 30);

        // Second session same day
        await _plantService.RecordReadingDayAsync(plant.Id, eveningSession, 45);

        // Assert - Should still be 1 reading day
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordReadingDayAsync_DifferentDays_ShouldRecordEachDay()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Multi Day Plant");
        var day1 = DateTime.UtcNow.Date;
        var day2 = day1.AddDays(1);
        var day3 = day1.AddDays(2);

        // Act - Three sessions on different days
        await _plantService.RecordReadingDayAsync(plant.Id, day1, 20);
        await _plantService.RecordReadingDayAsync(plant.Id, day2, 20);
        await _plantService.RecordReadingDayAsync(plant.Id, day3, 20);

        // Assert - Should have 3 reading days
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(3);
        updatedPlant.LastReadingDayRecorded!.Value.Date.Should().Be(day3);
    }

    [Fact]
    public async Task RecordReadingDayAsync_DeadPlant_ShouldNotRecord()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Dead Plant");
        plant.Status = PlantStatus.Dead;
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        // Act
        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow, 30);

        // Assert - Dead plants don't earn reading days
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(0);
        updatedPlant.LastReadingDayRecorded.Should().BeNull();
    }

    [Theory]
    [InlineData(PlantStatus.Healthy)]
    [InlineData(PlantStatus.Thirsty)]
    [InlineData(PlantStatus.Wilting)]
    public async Task RecordReadingDayAsync_AlivePlants_ShouldRecord(PlantStatus status)
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, $"{status} Plant");
        plant.Status = status;
        if (status == PlantStatus.Thirsty)
            plant.LastWatered = DateTime.UtcNow.AddDays(-4);
        else if (status == PlantStatus.Wilting)
            plant.LastWatered = DateTime.UtcNow.AddDays(-5);
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        // Act
        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow, 20);

        // Assert - All alive plants can earn reading days
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordReadingDayAsync_After3Days_ShouldLevelUpToLevel2()
    {
        // Arrange
        var species = await SeedPlantSpecies(growthRate: 1.0);
        var plant = await SeedUserPlant(species.Id, "Leveling Plant");
        var day1 = DateTime.UtcNow.Date;

        // Act - 3 reading days at GrowthRate 1.0 should reach level 2
        await _plantService.RecordReadingDayAsync(plant.Id, day1, 20);
        await _plantService.RecordReadingDayAsync(plant.Id, day1.AddDays(1), 20);
        await _plantService.RecordReadingDayAsync(plant.Id, day1.AddDays(2), 20);

        // Assert
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(3);
        updatedPlant.CurrentLevel.Should().Be(2);
    }

    [Fact]
    public async Task RecordReadingDayAsync_After6Days_ShouldLevelUpToLevel3()
    {
        // Arrange
        var species = await SeedPlantSpecies(growthRate: 1.0);
        var plant = await SeedUserPlant(species.Id, "Fast Leveling Plant");
        var startDay = DateTime.UtcNow.Date;

        // Act - 6 reading days at GrowthRate 1.0 should reach level 3
        for (int i = 0; i < 6; i++)
        {
            await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(i), 20);
        }

        // Assert
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(6);
        updatedPlant.CurrentLevel.Should().Be(3);
    }

    [Fact]
    public async Task RecordReadingDayAsync_WithFasterGrowthRate_ShouldLevelFaster()
    {
        // Arrange - GrowthRate 1.2 means faster leveling
        var species = await SeedPlantSpecies(growthRate: 1.2);
        var plant = await SeedUserPlant(species.Id, "Fast Growth Plant");
        var startDay = DateTime.UtcNow.Date;

        // Act - At GR 1.2, 5 days should give: floor(5 * 1.2 / 3) + 1 = floor(2) + 1 = 3
        for (int i = 0; i < 5; i++)
        {
            await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(i), 20);
        }

        // Assert
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(5);
        updatedPlant.CurrentLevel.Should().Be(3); // Level 3 at 5 days with GR 1.2
    }

    [Fact]
    public async Task RecordReadingDayAsync_WithSlowerGrowthRate_ShouldLevelSlower()
    {
        // Arrange - GrowthRate 0.8 means slower leveling
        var species = await SeedPlantSpecies(growthRate: 0.8);
        var plant = await SeedUserPlant(species.Id, "Slow Growth Plant");
        var startDay = DateTime.UtcNow.Date;

        // Act - At GR 0.8, 3 days should give: floor(3 * 0.8 / 3) + 1 = floor(0.8) + 1 = 1
        for (int i = 0; i < 3; i++)
        {
            await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(i), 20);
        }

        // Assert - Still level 1 after 3 days with slow growth rate
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(3);
        updatedPlant.CurrentLevel.Should().Be(1); // Still level 1 at 3 days with GR 0.8
    }

    [Fact]
    public async Task RecordReadingDayAsync_SlowGrowthAfter4Days_ShouldReachLevel2()
    {
        // Arrange - GrowthRate 0.8 needs 4 days for level 2
        var species = await SeedPlantSpecies(growthRate: 0.8);
        var plant = await SeedUserPlant(species.Id, "Slow But Steady Plant");
        var startDay = DateTime.UtcNow.Date;

        // Act - At GR 0.8, 4 days should give: floor(4 * 0.8 / 3) + 1 = floor(1.06) + 1 = 2
        for (int i = 0; i < 4; i++)
        {
            await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(i), 20);
        }

        // Assert
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(4);
        updatedPlant.CurrentLevel.Should().Be(2); // Level 2 at 4 days with GR 0.8
    }

    [Fact]
    public async Task RecordReadingDayAsync_ShouldNotExceedMaxLevel()
    {
        // Arrange - Species with max level 3
        var species = await SeedPlantSpecies(maxLevel: 3, growthRate: 1.0);
        var plant = await SeedUserPlant(species.Id, "Max Level Plant");
        var startDay = DateTime.UtcNow.Date;

        // Act - 100 reading days should hit max level 3
        for (int i = 0; i < 100; i++)
        {
            await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(i), 20);
        }

        // Assert
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(100);
        updatedPlant.CurrentLevel.Should().Be(3); // Capped at max level
    }

    [Fact]
    public async Task RecordReadingDayAsync_PlantNotFound_ShouldReturnSilently()
    {
        // Arrange - Non-existent plant ID
        var nonExistentId = Guid.NewGuid();

        // Act & Assert - Should not throw
        await _plantService.Invoking(p => p.RecordReadingDayAsync(nonExistentId, DateTime.UtcNow, 30))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordReadingDayAsync_ShouldOnlyIncreaseLevelNeverDecrease()
    {
        // Arrange - Plant that was manually set to higher level
        var species = await SeedPlantSpecies(growthRate: 1.0);
        var plant = await SeedUserPlant(species.Id, "High Level Plant");
        plant.CurrentLevel = 5; // Manually set higher than reading days would give
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        // Act - Add 1 reading day (would calculate to level 1)
        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow, 20);

        // Assert - Level should stay at 5, not decrease
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(1);
        updatedPlant.CurrentLevel.Should().Be(5); // Level should NOT decrease
    }

    [Fact]
    public async Task RecordReadingDayAsync_ZeroMinuteSession_ShouldNotRecord()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Zero Session Plant");

        // Act
        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow, 0);

        // Assert
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(0);
    }

    [Fact]
    public async Task RecordReadingDayAsync_NegativeMinutes_ShouldNotRecord()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Negative Session Plant");

        // Act
        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow, -10);

        // Assert
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(0);
    }

    [Fact]
    public async Task RecordReadingDayAsync_MixedSessionLengthsSameDay_ShouldOnlyCountOnce()
    {
        // Arrange
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Mixed Session Plant");
        var today = DateTime.UtcNow.Date;

        // Act - Multiple sessions of varying lengths on same day
        await _plantService.RecordReadingDayAsync(plant.Id, today.AddHours(8), 10);  // Too short
        await _plantService.RecordReadingDayAsync(plant.Id, today.AddHours(10), 20); // Long enough - recorded
        await _plantService.RecordReadingDayAsync(plant.Id, today.AddHours(14), 30); // Already recorded today
        await _plantService.RecordReadingDayAsync(plant.Id, today.AddHours(18), 5);  // Too short anyway

        // Assert - Should only count once
        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordReadingDayAsync_LevelProgressionIntegrationTest()
    {
        // Arrange - Full integration test simulating real usage
        var species = await SeedPlantSpecies(growthRate: 1.0, maxLevel: 10);
        var plant = await SeedUserPlant(species.Id, "Integration Test Plant");
        var startDay = DateTime.UtcNow.Date;

        // Act & Assert - Verify level progression over time
        // Day 1: Still level 1
        await _plantService.RecordReadingDayAsync(plant.Id, startDay, 20);
        var afterDay1 = await _plantService.GetByIdAsync(plant.Id);
        afterDay1!.CurrentLevel.Should().Be(1);

        // Day 2: Still level 1
        await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(1), 20);
        var afterDay2 = await _plantService.GetByIdAsync(plant.Id);
        afterDay2!.CurrentLevel.Should().Be(1);

        // Day 3: Level up to 2!
        await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(2), 20);
        var afterDay3 = await _plantService.GetByIdAsync(plant.Id);
        afterDay3!.CurrentLevel.Should().Be(2);

        // Day 6: Level up to 3!
        await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(3), 20);
        await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(4), 20);
        await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(5), 20);
        var afterDay6 = await _plantService.GetByIdAsync(plant.Id);
        afterDay6!.CurrentLevel.Should().Be(3);
        afterDay6.ReadingDaysCount.Should().Be(6);
    }

    #endregion

    #region Helper Methods

    private async Task<PlantSpecies> SeedPlantSpecies(
        string name = "Test Species",
        int unlockLevel = 1,
        bool isAvailable = true,
        double growthRate = 1.0,
        int maxLevel = 10)
    {
        var species = new PlantSpecies
        {
            Name = name,
            Description = "A test plant species",
            ImagePath = "/images/plants/test.svg",
            MaxLevel = maxLevel,
            WaterIntervalDays = 3,
            GrowthRate = growthRate,
            BaseCost = 100,
            UnlockLevel = unlockLevel,
            IsAvailable = isAvailable
        };

        var result = await _unitOfWork.PlantSpecies.AddAsync(species);
        await _dbHelper.Context.SaveChangesAsync();
        return result;
    }

    private async Task<UserPlant> SeedUserPlant(
        Guid speciesId,
        string name,
        bool isActive = false)
    {
        var plant = new UserPlant
        {
            SpeciesId = speciesId,
            Name = name,
            CurrentLevel = 1,
            Experience = 0,
            PlantedAt = DateTime.UtcNow,
            LastWatered = DateTime.UtcNow,
            IsActive = isActive,
            Status = PlantStatus.Healthy
        };

        var result = await _unitOfWork.UserPlants.AddAsync(plant);
        await _dbHelper.Context.SaveChangesAsync();
        return result;
    }

    #endregion
}
