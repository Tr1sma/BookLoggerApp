using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// Focused tests for the Ewiger Phönix-Bonsai mechanic:
/// - The phoenix self-revives if it would die.
/// - While the phoenix is owned, other plants cannot die.
/// </summary>
public class PlantServicePhoenixTests : IDisposable
{
    private readonly DbContextTestHelper _dbHelper;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AppSettingsProvider _settingsProvider;
    private readonly IDecorationService _decorationService;
    private readonly IMemoryCache _cache;
    private readonly PlantService _plantService;

    public PlantServicePhoenixTests()
    {
        _dbHelper = DbContextTestHelper.CreateTestContext();
        _unitOfWork = new UnitOfWork(_dbHelper.Context);
        var contextFactory = new TestDbContextFactory(_dbHelper.DatabaseName);
        _settingsProvider = new AppSettingsProvider(contextFactory);

        var services = new ServiceCollection();
        services.AddMemoryCache();
        _cache = services.BuildServiceProvider().GetRequiredService<IMemoryCache>();

        _decorationService = Substitute.For<IDecorationService>();
        _decorationService.UserOwnsAbilityAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        _plantService = new PlantService(_unitOfWork, _settingsProvider, _decorationService, _cache, NullLogger<PlantService>.Instance);
    }

    public void Dispose()
    {
        _dbHelper.Dispose();
    }

    [Fact]
    public async Task RefreshStatus_PlantWithoutPhoenix_DiesNormallyWhenOverdue()
    {
        var species = await SeedSpecies("Ordinary", SpecialAbilityKeys: null);
        var plant = await SeedPlant(species.Id, "Will die", lastWatered: DateTime.UtcNow.AddDays(-20));

        var refreshed = await _plantService.GetByIdAsync(plant.Id);

        refreshed!.Status.Should().Be(PlantStatus.Dead);
    }

    [Fact]
    public async Task RefreshStatus_WithEternalPhoenixAlive_OtherPlantCannotDie()
    {
        var phoenixSpecies = await SeedSpecies("Phönix", SpecialAbilityKeys.EternalPhoenix);
        var ordinarySpecies = await SeedSpecies("Ordinary", SpecialAbilityKeys: null);

        await SeedPlant(phoenixSpecies.Id, "Phönix", lastWatered: DateTime.UtcNow.AddDays(-1));
        var endangered = await SeedPlant(ordinarySpecies.Id, "Protected", lastWatered: DateTime.UtcNow.AddDays(-20));

        var refreshed = await _plantService.GetByIdAsync(endangered.Id);

        refreshed!.Status.Should().Be(PlantStatus.Wilting,
            "the Phönix prevents any other plant from dying while alive");
    }

    [Fact]
    public async Task RefreshStatus_PhoenixItself_SelfRevivesIfOverdue()
    {
        var phoenixSpecies = await SeedSpecies("Phönix", SpecialAbilityKeys.EternalPhoenix);
        var phoenix = await SeedPlant(phoenixSpecies.Id, "Phönix", lastWatered: DateTime.UtcNow.AddDays(-200));

        // Z.206: a pure read shows the protected (Wilting) status but must NOT mutate or persist
        // the phoenix's water timer — reads no longer write.
        var read = await _plantService.GetByIdAsync(phoenix.Id);
        read!.Status.Should().Be(PlantStatus.Wilting, "the phoenix must never be shown as Dead");
        read.LastWatered.Should().BeCloseTo(DateTime.UtcNow.AddDays(-200), TimeSpan.FromMinutes(1),
            "a read must not reset the phoenix's water timer (Z.206 write-on-read fix)");

        // The maintenance pass is the real persist path: it self-revives the phoenix and resets
        // its water timer, so the next load sees a freshly-watered plant.
        await _plantService.UpdatePlantStatusesAsync();

        var afterMaintenance = await _plantService.GetByIdAsync(phoenix.Id);
        afterMaintenance!.LastWatered.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5),
            "the maintenance pass resets the phoenix's water timer on self-revival");
    }

    [Fact]
    public async Task RefreshStatus_OtherPlantCountsAsAliveEvenIfPhoenixIsHealthy()
    {
        var phoenixSpecies = await SeedSpecies("Phönix", SpecialAbilityKeys.EternalPhoenix);
        var ordinarySpecies = await SeedSpecies("Ordinary", SpecialAbilityKeys: null);

        await SeedPlant(phoenixSpecies.Id, "Phönix", lastWatered: DateTime.UtcNow);
        var other = await SeedPlant(ordinarySpecies.Id, "Fern", lastWatered: DateTime.UtcNow.AddDays(-20));

        var refreshed = await _plantService.GetByIdAsync(other.Id);

        refreshed!.Status.Should().NotBe(PlantStatus.Dead);
    }

    private async Task<PlantSpecies> SeedSpecies(string name, string? SpecialAbilityKeys)
    {
        var species = new PlantSpecies
        {
            Name = name,
            Description = name,
            ImagePath = "images/plants/test.svg",
            MaxLevel = 10,
            WaterIntervalDays = 3,
            GrowthRate = 1.0,
            XpBoostPercentage = 0.1m,
            BaseCost = 100,
            UnlockLevel = 1,
            IsAvailable = true,
            SpecialAbilityKey = SpecialAbilityKeys
        };
        await _unitOfWork.PlantSpecies.AddAsync(species);
        await _dbHelper.Context.SaveChangesAsync();
        return species;
    }

    private async Task<UserPlant> SeedPlant(Guid speciesId, string name, DateTime lastWatered)
    {
        var plant = new UserPlant
        {
            SpeciesId = speciesId,
            Name = name,
            CurrentLevel = 1,
            Experience = 0,
            PlantedAt = DateTime.UtcNow.AddDays(-30),
            LastWatered = lastWatered,
            Status = PlantStatus.Healthy
        };
        await _unitOfWork.UserPlants.AddAsync(plant);
        await _dbHelper.Context.SaveChangesAsync();
        return plant;
    }
}
