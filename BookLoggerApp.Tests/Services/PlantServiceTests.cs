using FluentAssertions;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class PlantServiceTests : IDisposable
{
    private readonly PlantService _plantService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DbContextTestHelper _dbHelper;
    private readonly IMemoryCache _cache;
    private readonly AppSettingsProvider _settingsProvider;

    public PlantServiceTests()
    {
        _dbHelper = DbContextTestHelper.CreateTestContext();

        var services = new ServiceCollection();
        services.AddMemoryCache();
        var serviceProvider = services.BuildServiceProvider();
        _cache = serviceProvider.GetRequiredService<IMemoryCache>();

        _unitOfWork = new UnitOfWork(_dbHelper.Context);
        var contextFactory = new TestDbContextFactory(_dbHelper.DatabaseName);
        _settingsProvider = new AppSettingsProvider(contextFactory);
        var logger = NullLogger<PlantService>.Instance;
        var decorationService = Substitute.For<IDecorationService>();
        decorationService.UserOwnsAbilityAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        _plantService = new PlantService(_unitOfWork, _settingsProvider, decorationService, _cache, logger);
    }

    public void Dispose()
    {
        _dbHelper.Dispose();
    }

    #region Basic CRUD Tests

    [Fact]
    public async Task AddAsync_ShouldCreatePlant()
    {
        var species = await SeedPlantSpecies();
        var plant = new UserPlant
        {
            SpeciesId = species.Id,
            Name = "Test Plant",
            CurrentLevel = 1,
            Experience = 0,
            IsActive = true
        };

        var result = await _plantService.AddAsync(plant);

        result.Should().NotBeNull();
        result.Name.Should().Be("Test Plant");
        result.PlantedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.LastWatered.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnPlant()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "My Plant");

        var result = await _plantService.GetByIdAsync(plant.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("My Plant");
        result.Species.Should().NotBeNull();
        result.Species.Name.Should().Be("Test Species");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllPlants()
    {
        var species = await SeedPlantSpecies();
        await SeedUserPlant(species.Id, "Plant 1");
        await SeedUserPlant(species.Id, "Plant 2");

        var result = await _plantService.GetAllAsync();

        result.Should().HaveCount(2);
        result.Select(p => p.Name).Should().Contain(new[] { "Plant 1", "Plant 2" });
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemovePlantAndShelfLinks()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Shelf Plant");
        var shelf = new Shelf
        {
            Id = Guid.NewGuid(),
            Name = "Main Shelf",
            SortOrder = 0
        };

        await _dbHelper.Context.Shelves.AddAsync(shelf);
        await _dbHelper.Context.PlantShelves.AddAsync(new PlantShelf
        {
            PlantId = plant.Id,
            ShelfId = shelf.Id,
            Position = 0
        });
        await _dbHelper.Context.SaveChangesAsync();
        _dbHelper.Context.ChangeTracker.Clear();

        await _plantService.DeleteAsync(plant.Id);
        _dbHelper.Context.ChangeTracker.Clear();

        var deletedPlant = await _dbHelper.Context.UserPlants.FindAsync(plant.Id);
        var shelfLinks = _dbHelper.Context.PlantShelves.Where(ps => ps.PlantId == plant.Id).ToList();

        deletedPlant.Should().BeNull();
        shelfLinks.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_WhenDeletingActivePlant_ShouldClearActivePlant()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Active Plant", isActive: true);

        await _plantService.DeleteAsync(plant.Id);

        var activePlant = await _plantService.GetActivePlantAsync();
        activePlant.Should().BeNull();
    }

    #endregion

    #region Active Plant Tests

    [Fact]
    public async Task SetActivePlantAsync_ShouldActivatePlantAndDeactivateOthers()
    {
        var species = await SeedPlantSpecies();
        var plant1 = await SeedUserPlant(species.Id, "Plant 1", isActive: true);
        var plant2 = await SeedUserPlant(species.Id, "Plant 2", isActive: false);

        _dbHelper.Context.ChangeTracker.Clear(); // avoid tracking conflicts

        await _plantService.SetActivePlantAsync(plant2.Id);

        var activePlant = await _plantService.GetActivePlantAsync();
        activePlant.Should().NotBeNull();
        activePlant!.Id.Should().Be(plant2.Id);

        var plant1Updated = await _plantService.GetByIdAsync(plant1.Id);
        plant1Updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetActivePlantAsync_WhenNoActivePlant_ShouldReturnNull()
    {
        var result = await _plantService.GetActivePlantAsync();

        result.Should().BeNull();
    }

    #endregion

    #region Watering Tests

    [Fact]
    public async Task WaterPlantAsync_ShouldUpdateLastWateredAndStatus()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Thirsty Plant");

        plant.LastWatered = DateTime.UtcNow.AddDays(-4);
        plant.Status = PlantStatus.Thirsty;
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        await _plantService.WaterPlantAsync(plant.Id);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant.Should().NotBeNull();
        updatedPlant!.LastWatered.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        updatedPlant.Status.Should().Be(PlantStatus.Healthy);
    }

    [Fact]
    public async Task WaterPlantAsync_WhenPlantIsDead_ShouldThrowException()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Dead Plant");
        // Death derived from timestamp, not manually assigned status.
        plant.LastWatered = DateTime.UtcNow.AddDays(-10);
        plant.Status = PlantStatus.Dead;
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        Func<Task> act = async () => await _plantService.WaterPlantAsync(plant.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot water a dead plant");
    }

    [Fact]
    public async Task WaterPlantAsync_WhenPlantHasBecomeDeadSinceLastStatusUpdate_ShouldThrowException()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Stale Dead Plant");
        plant.LastWatered = DateTime.UtcNow.AddDays(-10);
        plant.Status = PlantStatus.Healthy;
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        Func<Task> act = async () => await _plantService.WaterPlantAsync(plant.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot water a dead plant");

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.Status.Should().Be(PlantStatus.Dead);
    }

    #endregion

    #region Experience & Leveling Tests

    [Fact]
    public async Task AddExperienceAsync_ShouldIncreaseExperience()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Growing Plant");

        await _plantService.AddExperienceAsync(plant.Id, 50);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.Experience.Should().Be(50);
    }

    [Fact]
    public async Task AddExperienceAsync_WhenEnoughForLevelUp_ShouldIncreaseLevel()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Leveling Plant");

        // 150 XP needed for level 2: 100 * 1.5^1
        await _plantService.AddExperienceAsync(plant.Id, 150);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.CurrentLevel.Should().Be(2);
        updatedPlant.Experience.Should().Be(150);
    }

    [Fact]
    public async Task AddExperienceAsync_WhenEnoughForMultipleLevels_ShouldLevelUpCorrectly()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Fast Growing Plant");

        // 375 XP = 150 for L2 + 225 for L3
        await _plantService.AddExperienceAsync(plant.Id, 375);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.CurrentLevel.Should().Be(3);
        updatedPlant.Experience.Should().Be(375);
    }

    [Fact]
    public async Task CanLevelUpAsync_WhenEnoughXp_ShouldReturnTrue()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Ready Plant");
        plant.Experience = 150; // enough for level 2
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        var canLevel = await _plantService.CanLevelUpAsync(plant.Id);

        canLevel.Should().BeTrue();
    }

    [Fact]
    public async Task CanLevelUpAsync_WhenNotEnoughXp_ShouldReturnFalse()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Not Ready Plant");
        plant.Experience = 50; // not enough for level 2
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        var canLevel = await _plantService.CanLevelUpAsync(plant.Id);

        canLevel.Should().BeFalse();
    }

    [Fact]
    public async Task CanLevelUpAsync_WhenPlantHasBecomeDeadSinceLastStatusUpdate_ShouldReturnFalse()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Dead Level Plant");
        plant.Experience = 999;
        plant.LastWatered = DateTime.UtcNow.AddDays(-10);
        plant.Status = PlantStatus.Healthy;
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        var canLevel = await _plantService.CanLevelUpAsync(plant.Id);

        canLevel.Should().BeFalse();
    }

    [Fact]
    public async Task LevelUpAsync_WhenPlantHasBecomeDeadSinceLastStatusUpdate_ShouldThrowException()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Dead Manual Level Plant");
        plant.Experience = 999;
        plant.LastWatered = DateTime.UtcNow.AddDays(-10);
        plant.Status = PlantStatus.Healthy;
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        Func<Task> act = async () => await _plantService.LevelUpAsync(plant.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot level up a dead plant");
    }

    #endregion

    #region XP Boost Tests

    [Fact]
    public async Task CalculateTotalXpBoostAsync_HigherLevelPlant_ShouldReturnHigherBoost()
    {
        var species = await SeedPlantSpecies(maxLevel: 10);
        var plant = await SeedUserPlant(species.Id, "Boost Plant");

        var levelOneBoost = await _plantService.CalculateTotalXpBoostAsync();

        plant.CurrentLevel = 5;
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        var levelFiveBoost = await _plantService.CalculateTotalXpBoostAsync();

        levelOneBoost.Should().Be(0.055m);
        levelFiveBoost.Should().Be(0.075m);
        levelFiveBoost.Should().BeGreaterThan(levelOneBoost);
    }

    [Fact]
    public async Task CalculateTotalXpBoostAsync_DeadPlants_ShouldNotContributeToBoost()
    {
        var species = await SeedPlantSpecies(maxLevel: 10);
        var healthyPlant = await SeedUserPlant(species.Id, "Healthy Boost Plant");
        healthyPlant.CurrentLevel = 5;
        await _unitOfWork.UserPlants.UpdateAsync(healthyPlant);

        var deadPlant = await SeedUserPlant(species.Id, "Dead Boost Plant");
        deadPlant.CurrentLevel = 10;
        deadPlant.LastWatered = DateTime.UtcNow.AddDays(-10);
        deadPlant.Status = PlantStatus.Dead;
        await _unitOfWork.UserPlants.UpdateAsync(deadPlant);

        var totalBoost = await _plantService.CalculateTotalXpBoostAsync();

        totalBoost.Should().Be(0.075m);
    }

    #endregion

    #region Purchase Tests

    [Fact]
    public async Task PurchasePlantAsync_ShouldCreateNewPlant()
    {
        var species = await SeedPlantSpecies();

        var plant = await _plantService.PurchasePlantAsync(species.Id, "Purchased Plant");

        plant.Should().NotBeNull();
        plant.Name.Should().Be("Purchased Plant");
        plant.SpeciesId.Should().Be(species.Id);
        plant.CurrentLevel.Should().Be(1);
        plant.Experience.Should().Be(0);
    }

    [Fact]
    public async Task PurchasePlantAsync_WhenSpeciesNotAvailable_ShouldThrowException()
    {
        var species = await SeedPlantSpecies(isAvailable: false);

        Func<Task> act = async () => await _plantService.PurchasePlantAsync(species.Id, "Test");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Plant species is not available for purchase");
    }

    [Fact]
    public async Task PurchasePlantAsync_OnSuccess_ShouldDeductCoinsAndIncrementPlantsPurchased()
    {
        var species = await SeedPlantSpecies(); // BaseCost = 100
        var initialCoins = await _settingsProvider.GetUserCoinsAsync();
        var initialPurchased = await _settingsProvider.GetPlantsPurchasedAsync();
        int expectedCost = 100; // BaseCost + (0 * 200)

        await _plantService.PurchasePlantAsync(species.Id, "Bought Plant");

        var finalCoins = await _settingsProvider.GetUserCoinsAsync();
        var finalPurchased = await _settingsProvider.GetPlantsPurchasedAsync();
        finalCoins.Should().Be(initialCoins - expectedCost);
        finalPurchased.Should().Be(initialPurchased + 1);
    }

    [Fact]
    public async Task PurchasePlantAsync_WhenPlantSaveFails_ShouldRefundCoinsAndNotIncrementCounter()
    {
        var species = await SeedPlantSpecies(); // seeded via real UoW, mock returns it directly
        var initialCoins = await _settingsProvider.GetUserCoinsAsync();
        var initialPurchased = await _settingsProvider.GetPlantsPurchasedAsync();

        var failingService = CreatePlantServiceWithFailingSave(
            configureMock: mock =>
            {
                mock.PlantSpecies.GetByIdAsync(species.Id, Arg.Any<CancellationToken>()).Returns(species);
                mock.UserPlants.AddAsync(Arg.Any<UserPlant>(), Arg.Any<CancellationToken>())
                    .Returns(ci => ci.Arg<UserPlant>());
            });

        await failingService.Invoking(s => s.PurchasePlantAsync(species.Id, "Doomed Plant"))
            .Should().ThrowAsync<DbUpdateException>();

        // Coins refunded; counter not advanced (dynamic pricing must stay consistent)
        var finalCoins = await _settingsProvider.GetUserCoinsAsync();
        finalCoins.Should().Be(initialCoins);

        var finalPurchased = await _settingsProvider.GetPlantsPurchasedAsync();
        finalPurchased.Should().Be(initialPurchased);
    }

    [Fact]
    public async Task PurchaseLevelAsync_WhenPlantSaveFails_ShouldRefundCoins()
    {
        // Ensure AppSettings exist, then top up
        _ = await _settingsProvider.GetUserCoinsAsync();
        await _settingsProvider.AddCoinsAsync(500);
        var initialCoins = await _settingsProvider.GetUserCoinsAsync();

        var species = new PlantSpecies
        {
            Id = Guid.NewGuid(),
            Name = "Inline Species",
            MaxLevel = 5,
            WaterIntervalDays = 3,
            GrowthRate = 1.0,
            BaseCost = 100,
            UnlockLevel = 1,
            IsAvailable = true
        };
        var plant = new UserPlant
        {
            Id = Guid.NewGuid(),
            SpeciesId = species.Id,
            Species = species,
            Name = "Level-Up Target",
            CurrentLevel = 1, // cost = (1+1) * 100 = 200
            Experience = 0,
            PlantedAt = DateTime.UtcNow,
            LastWatered = DateTime.UtcNow,
            Status = PlantStatus.Healthy
        };

        var failingService = CreatePlantServiceWithFailingSave(
            configureMock: mock =>
            {
                mock.UserPlants.GetPlantWithSpeciesAsync(plant.Id).Returns(plant);
                mock.UserPlants.GetUserPlantsAsync().Returns(new List<UserPlant> { plant });
            });

        await failingService.Invoking(s => s.PurchaseLevelAsync(plant.Id))
            .Should().ThrowAsync<DbUpdateException>();

        var finalCoins = await _settingsProvider.GetUserCoinsAsync();
        finalCoins.Should().Be(initialCoins);
    }

    private PlantService CreatePlantServiceWithFailingSave(Action<IUnitOfWork> configureMock)
    {
        var mockUoW = Substitute.For<IUnitOfWork>();
        configureMock(mockUoW);
        mockUoW.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new DbUpdateException("simulated save failure")));

        var decorationService = Substitute.For<IDecorationService>();
        decorationService.UserOwnsAbilityAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        return new PlantService(
            mockUoW,
            _settingsProvider,
            decorationService,
            _cache,
            NullLogger<PlantService>.Instance);
    }

    #endregion

    #region Plant Status Updates

    [Fact]
    public async Task UpdatePlantStatusesAsync_ShouldUpdateStatusBasedOnLastWatered()
    {
        var species = await SeedPlantSpecies();
        var healthyPlant = await SeedUserPlant(species.Id, "Healthy Plant");

        var thirstyPlant = await SeedUserPlant(species.Id, "Thirsty Plant");
        thirstyPlant.LastWatered = DateTime.UtcNow.AddDays(-3.5);
        await _unitOfWork.UserPlants.UpdateAsync(thirstyPlant);

        await _plantService.UpdatePlantStatusesAsync();

        var healthyUpdated = await _plantService.GetByIdAsync(healthyPlant.Id);
        healthyUpdated!.Status.Should().Be(PlantStatus.Healthy);

        var thirstyUpdated = await _plantService.GetByIdAsync(thirstyPlant.Id);
        thirstyUpdated!.Status.Should().Be(PlantStatus.Thirsty);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldRefreshStaleStatusBeforeReturningPlant()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Stale Plant");
        plant.LastWatered = DateTime.UtcNow.AddDays(-5);
        plant.Status = PlantStatus.Healthy;
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        var refreshedPlant = await _plantService.GetByIdAsync(plant.Id);

        refreshedPlant.Should().NotBeNull();
        refreshedPlant!.Status.Should().Be(PlantStatus.Wilting);
    }

    [Fact]
    public async Task GetPlantsNeedingWaterAsync_ShouldReturnOnlyPlantsNeedingWater()
    {
        var species = await SeedPlantSpecies();
        var healthyPlant = await SeedUserPlant(species.Id, "Healthy Plant");

        var needsWaterPlant = await SeedUserPlant(species.Id, "Needs Water");
        needsWaterPlant.LastWatered = DateTime.UtcNow.AddHours(-67); // within 6 h of threshold
        await _unitOfWork.UserPlants.UpdateAsync(needsWaterPlant);

        var plantsNeedingWater = await _plantService.GetPlantsNeedingWaterAsync();

        plantsNeedingWater.Should().ContainSingle();
        plantsNeedingWater.First().Name.Should().Be("Needs Water");
    }

    #endregion

    #region Species Tests

    [Fact]
    public async Task GetAvailableSpeciesAsync_ShouldReturnOnlyAvailableSpeciesForUserLevel()
    {
        // DB may contain seeded species; filter by name prefix
        var species1 = await SeedPlantSpecies("Test Species 1", unlockLevel: 1, isAvailable: true);
        var species2 = await SeedPlantSpecies("Test Species 2", unlockLevel: 5, isAvailable: true);
        var species3 = await SeedPlantSpecies("Test Species 3", unlockLevel: 10, isAvailable: false);
        var species4 = await SeedPlantSpecies("Test Species 4", unlockLevel: 15, isAvailable: true);

        var result = await _plantService.GetAvailableSpeciesAsync(userLevel: 5);

        result.Select(s => s.Name).Should().Contain(new[] { "Test Species 1", "Test Species 2" });
        result.Select(s => s.Name).Should().NotContain("Test Species 3");
        result.Select(s => s.Name).Should().NotContain("Test Species 4");
        result.Should().OnlyContain(s => s.IsAvailable && s.UnlockLevel <= 5);
    }

    #endregion

    #region Reading Days Tests (RecordReadingDayAsync)

    [Fact]
    public async Task RecordReadingDayAsync_WithLessThan15Minutes_ShouldNotRecord()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Short Session Plant");

        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow, 14);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(0);
        updatedPlant.LastReadingDayRecorded.Should().BeNull();
    }

    [Fact]
    public async Task RecordReadingDayAsync_WithExactly15Minutes_ShouldRecord()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Exact Session Plant");
        var sessionDate = DateTime.UtcNow;

        await _plantService.RecordReadingDayAsync(plant.Id, sessionDate, 15);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(1);
        updatedPlant.LastReadingDayRecorded.Should().NotBeNull();
        updatedPlant.LastReadingDayRecorded!.Value.Date.Should().Be(sessionDate.Date);
    }

    [Fact]
    public async Task RecordReadingDayAsync_WithMoreThan15Minutes_ShouldRecord()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Long Session Plant");
        var sessionDate = DateTime.UtcNow;

        await _plantService.RecordReadingDayAsync(plant.Id, sessionDate, 60);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(1);
        updatedPlant.LastReadingDayRecorded.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordReadingDayAsync_FirstReadingDay_ShouldRecordWhenLastRecordedIsNull()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "First Day Plant");

        var initialPlant = await _plantService.GetByIdAsync(plant.Id);
        initialPlant!.LastReadingDayRecorded.Should().BeNull();
        initialPlant.ReadingDaysCount.Should().Be(0);

        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow, 20);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(1);
        updatedPlant.LastReadingDayRecorded.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordReadingDayAsync_SameDaySecondSession_ShouldNotRecordAgain()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Same Day Plant");
        var today = DateTime.UtcNow.Date;

        await _plantService.RecordReadingDayAsync(plant.Id, today.AddHours(8), 30);
        await _plantService.RecordReadingDayAsync(plant.Id, today.AddHours(20), 45);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordReadingDayAsync_DifferentDays_ShouldRecordEachDay()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Multi Day Plant");
        var day1 = DateTime.UtcNow.Date;
        var day2 = day1.AddDays(1);
        var day3 = day1.AddDays(2);

        await _plantService.RecordReadingDayAsync(plant.Id, day1, 20);
        await _plantService.RecordReadingDayAsync(plant.Id, day2, 20);
        await _plantService.RecordReadingDayAsync(plant.Id, day3, 20);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(3);
        updatedPlant.LastReadingDayRecorded!.Value.Date.Should().Be(day3);
    }

    [Fact]
    public async Task RecordReadingDayAsync_DeadPlant_ShouldNotRecord()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Dead Plant");
        // Death derived from timestamp, not manually assigned status.
        plant.LastWatered = DateTime.UtcNow.AddDays(-10);
        plant.Status = PlantStatus.Dead;
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow, 30);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(0);
        updatedPlant.LastReadingDayRecorded.Should().BeNull();
    }

    [Fact]
    public async Task RecordReadingDayAsync_WhenPlantHasBecomeDeadSinceLastStatusUpdate_ShouldNotRecord()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Stale Dead Plant");
        plant.LastWatered = DateTime.UtcNow.AddDays(-10);
        plant.Status = PlantStatus.Healthy;
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow, 30);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.Status.Should().Be(PlantStatus.Dead);
        updatedPlant.ReadingDaysCount.Should().Be(0);
        updatedPlant.LastReadingDayRecorded.Should().BeNull();
    }

    [Theory]
    [InlineData(PlantStatus.Healthy)]
    [InlineData(PlantStatus.Thirsty)]
    [InlineData(PlantStatus.Wilting)]
    public async Task RecordReadingDayAsync_AlivePlants_ShouldRecord(PlantStatus status)
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, $"{status} Plant");
        plant.Status = status;
        if (status == PlantStatus.Thirsty)
            plant.LastWatered = DateTime.UtcNow.AddDays(-4);
        else if (status == PlantStatus.Wilting)
            plant.LastWatered = DateTime.UtcNow.AddDays(-5);
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow, 20);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordReadingDayAsync_After3Days_ShouldLevelUpToLevel2()
    {
        var species = await SeedPlantSpecies(growthRate: 1.0);
        var plant = await SeedUserPlant(species.Id, "Leveling Plant");
        var day1 = DateTime.UtcNow.Date;

        // GrowthRate 1.0: level = floor(days / 3) + 1
        await _plantService.RecordReadingDayAsync(plant.Id, day1, 20);
        await _plantService.RecordReadingDayAsync(plant.Id, day1.AddDays(1), 20);
        await _plantService.RecordReadingDayAsync(plant.Id, day1.AddDays(2), 20);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(3);
        updatedPlant.CurrentLevel.Should().Be(2);
    }

    [Fact]
    public async Task RecordReadingDayAsync_After6Days_ShouldLevelUpToLevel3()
    {
        var species = await SeedPlantSpecies(growthRate: 1.0);
        var plant = await SeedUserPlant(species.Id, "Fast Leveling Plant");
        var startDay = DateTime.UtcNow.Date;

        for (int i = 0; i < 6; i++)
        {
            await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(i), 20);
        }

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(6);
        updatedPlant.CurrentLevel.Should().Be(3);
    }

    [Fact]
    public async Task RecordReadingDayAsync_WithFasterGrowthRate_ShouldLevelFaster()
    {
        // GR 1.2: floor(5 * 1.2 / 3) + 1 = floor(2) + 1 = 3
        var species = await SeedPlantSpecies(growthRate: 1.2);
        var plant = await SeedUserPlant(species.Id, "Fast Growth Plant");
        var startDay = DateTime.UtcNow.Date;

        for (int i = 0; i < 5; i++)
        {
            await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(i), 20);
        }

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(5);
        updatedPlant.CurrentLevel.Should().Be(3);
    }

    [Fact]
    public async Task RecordReadingDayAsync_WithSlowerGrowthRate_ShouldLevelSlower()
    {
        // GR 0.8: floor(3 * 0.8 / 3) + 1 = floor(0.8) + 1 = 1
        var species = await SeedPlantSpecies(growthRate: 0.8);
        var plant = await SeedUserPlant(species.Id, "Slow Growth Plant");
        var startDay = DateTime.UtcNow.Date;

        for (int i = 0; i < 3; i++)
        {
            await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(i), 20);
        }

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(3);
        updatedPlant.CurrentLevel.Should().Be(1);
    }

    [Fact]
    public async Task RecordReadingDayAsync_SlowGrowthAfter4Days_ShouldReachLevel2()
    {
        // GR 0.8: floor(4 * 0.8 / 3) + 1 = floor(1.06) + 1 = 2
        var species = await SeedPlantSpecies(growthRate: 0.8);
        var plant = await SeedUserPlant(species.Id, "Slow But Steady Plant");
        var startDay = DateTime.UtcNow.Date;

        for (int i = 0; i < 4; i++)
        {
            await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(i), 20);
        }

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(4);
        updatedPlant.CurrentLevel.Should().Be(2);
    }

    [Fact]
    public async Task RecordReadingDayAsync_ShouldNotExceedMaxLevel()
    {
        var species = await SeedPlantSpecies(maxLevel: 3, growthRate: 1.0);
        var plant = await SeedUserPlant(species.Id, "Max Level Plant");
        var startDay = DateTime.UtcNow.Date;

        for (int i = 0; i < 100; i++)
        {
            await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(i), 20);
        }

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(100);
        updatedPlant.CurrentLevel.Should().Be(3);
    }

    [Fact]
    public async Task RecordReadingDayAsync_PlantNotFound_ShouldReturnSilently()
    {
        var nonExistentId = Guid.NewGuid();

        await _plantService.Invoking(p => p.RecordReadingDayAsync(nonExistentId, DateTime.UtcNow, 30))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordReadingDayAsync_ShouldOnlyIncreaseLevelNeverDecrease()
    {
        var species = await SeedPlantSpecies(growthRate: 1.0);
        var plant = await SeedUserPlant(species.Id, "High Level Plant");
        plant.CurrentLevel = 5; // higher than 1 reading day would yield
        await _unitOfWork.UserPlants.UpdateAsync(plant);

        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow, 20);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(1);
        updatedPlant.CurrentLevel.Should().Be(5);
    }

    [Fact]
    public async Task RecordReadingDayAsync_ZeroMinuteSession_ShouldNotRecord()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Zero Session Plant");

        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow, 0);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(0);
    }

    [Fact]
    public async Task RecordReadingDayAsync_NegativeMinutes_ShouldNotRecord()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Negative Session Plant");

        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow, -10);

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(0);
    }

    [Fact]
    public async Task RecordReadingDayAsync_MixedSessionLengthsSameDay_ShouldOnlyCountOnce()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Mixed Session Plant");
        var today = DateTime.UtcNow.Date;

        await _plantService.RecordReadingDayAsync(plant.Id, today.AddHours(8), 10);  // too short
        await _plantService.RecordReadingDayAsync(plant.Id, today.AddHours(10), 20); // recorded
        await _plantService.RecordReadingDayAsync(plant.Id, today.AddHours(14), 30); // already recorded
        await _plantService.RecordReadingDayAsync(plant.Id, today.AddHours(18), 5);  // too short

        var updatedPlant = await _plantService.GetByIdAsync(plant.Id);
        updatedPlant!.ReadingDaysCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordReadingDayAsync_LevelProgressionIntegrationTest()
    {
        var species = await SeedPlantSpecies(growthRate: 1.0, maxLevel: 10);
        var plant = await SeedUserPlant(species.Id, "Integration Test Plant");
        var startDay = DateTime.UtcNow.Date;

        await _plantService.RecordReadingDayAsync(plant.Id, startDay, 20);
        (await _plantService.GetByIdAsync(plant.Id))!.CurrentLevel.Should().Be(1);

        await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(1), 20);
        (await _plantService.GetByIdAsync(plant.Id))!.CurrentLevel.Should().Be(1);

        await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(2), 20);
        (await _plantService.GetByIdAsync(plant.Id))!.CurrentLevel.Should().Be(2);

        await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(3), 20);
        await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(4), 20);
        await _plantService.RecordReadingDayAsync(plant.Id, startDay.AddDays(5), 20);
        var afterDay6 = await _plantService.GetByIdAsync(plant.Id);
        afterDay6!.CurrentLevel.Should().Be(3);
        afterDay6.ReadingDaysCount.Should().Be(6);
    }

    #endregion

    #region Coin Reward Tests

    [Fact]
    public async Task AddExperienceAsync_WhenLevelUp_ShouldAwardCoins()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Coin Earning Plant");
        var initialCoins = await _settingsProvider.GetUserCoinsAsync();

        // 150 XP → level 2
        await _plantService.AddExperienceAsync(plant.Id, 150);

        var finalCoins = await _settingsProvider.GetUserCoinsAsync();
        finalCoins.Should().Be(initialCoins + 100);
    }

    [Fact]
    public async Task AddExperienceAsync_WhenMultipleLevelUps_ShouldAwardCoinsForEachLevel()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "Multi Level Coin Plant");
        var initialCoins = await _settingsProvider.GetUserCoinsAsync();

        // 375 XP = 150 for L2 + 225 for L3 → 2 * 100 coins
        await _plantService.AddExperienceAsync(plant.Id, 375);

        var finalCoins = await _settingsProvider.GetUserCoinsAsync();
        finalCoins.Should().Be(initialCoins + 200);
    }

    [Fact]
    public async Task AddExperienceAsync_WhenNoLevelUp_ShouldNotAwardCoins()
    {
        var species = await SeedPlantSpecies();
        var plant = await SeedUserPlant(species.Id, "No Level Plant");
        var initialCoins = await _settingsProvider.GetUserCoinsAsync();

        await _plantService.AddExperienceAsync(plant.Id, 50);

        var finalCoins = await _settingsProvider.GetUserCoinsAsync();
        finalCoins.Should().Be(initialCoins);
    }

    [Fact]
    public async Task RecordReadingDayAsync_WhenLevelUp_ShouldAwardCoins()
    {
        var species = await SeedPlantSpecies(growthRate: 1.0);
        var plant = await SeedUserPlant(species.Id, "Reading Coin Plant");
        var day1 = DateTime.UtcNow.Date;
        var initialCoins = await _settingsProvider.GetUserCoinsAsync();

        // 3 days → level 2 → 100 coins
        await _plantService.RecordReadingDayAsync(plant.Id, day1, 20);
        await _plantService.RecordReadingDayAsync(plant.Id, day1.AddDays(1), 20);
        await _plantService.RecordReadingDayAsync(plant.Id, day1.AddDays(2), 20);

        var finalCoins = await _settingsProvider.GetUserCoinsAsync();
        finalCoins.Should().Be(initialCoins + 100);
    }

    [Fact]
    public async Task RecordReadingDayAsync_WhenNoLevelUp_ShouldNotAwardCoins()
    {
        var species = await SeedPlantSpecies(growthRate: 1.0);
        var plant = await SeedUserPlant(species.Id, "No Level Reading Plant");
        var initialCoins = await _settingsProvider.GetUserCoinsAsync();

        await _plantService.RecordReadingDayAsync(plant.Id, DateTime.UtcNow.Date, 20);

        var finalCoins = await _settingsProvider.GetUserCoinsAsync();
        finalCoins.Should().Be(initialCoins);
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
