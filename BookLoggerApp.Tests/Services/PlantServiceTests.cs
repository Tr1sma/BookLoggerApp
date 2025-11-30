using FluentAssertions;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class PlantServiceTests : IDisposable
{
    private readonly PlantService _plantService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserPlantRepository _userPlantRepository;
    private readonly Repository<PlantSpecies> _speciesRepository;
    private readonly DbContextTestHelper _dbHelper;
    private readonly IMemoryCache _cache;

    public PlantServiceTests()
    {
        _dbHelper = DbContextTestHelper.CreateTestContext();
        var bookRepository = new BookRepository(_dbHelper.Context);
        var sessionRepository = new ReadingSessionRepository(_dbHelper.Context);
        var goalRepository = new ReadingGoalRepository(_dbHelper.Context);
        _userPlantRepository = new UserPlantRepository(_dbHelper.Context);
        _speciesRepository = new Repository<PlantSpecies>(_dbHelper.Context);

        // Create memory cache for testing
        var services = new ServiceCollection();
        services.AddMemoryCache();
        var serviceProvider = services.BuildServiceProvider();
        _cache = serviceProvider.GetRequiredService<IMemoryCache>();

        _unitOfWork = new UnitOfWork(_dbHelper.Context, bookRepository, sessionRepository, goalRepository, _userPlantRepository);
        var settingsProvider = new AppSettingsProvider(_dbHelper.Context);
        _plantService = new PlantService(_unitOfWork, _speciesRepository, settingsProvider, _dbHelper.Context, _cache, null!);
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
        await _userPlantRepository.UpdateAsync(plant);

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
        await _userPlantRepository.UpdateAsync(plant);

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
        await _userPlantRepository.UpdateAsync(plant);

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
        await _userPlantRepository.UpdateAsync(plant);

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
        await _userPlantRepository.UpdateAsync(thirstyPlant);

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
        await _userPlantRepository.UpdateAsync(needsWaterPlant);

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

    #region Helper Methods

    private async Task<PlantSpecies> SeedPlantSpecies(
        string name = "Test Species",
        int unlockLevel = 1,
        bool isAvailable = true)
    {
        var species = new PlantSpecies
        {
            Name = name,
            Description = "A test plant species",
            ImagePath = "/images/plants/test.svg",
            MaxLevel = 10,
            WaterIntervalDays = 3,
            GrowthRate = 1.0,
            BaseCost = 100,
            UnlockLevel = unlockLevel,
            IsAvailable = isAvailable
        };

        var result = await _speciesRepository.AddAsync(species);
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

        var result = await _userPlantRepository.AddAsync(plant);
        await _dbHelper.Context.SaveChangesAsync();
        return result;
    }

    #endregion
}
