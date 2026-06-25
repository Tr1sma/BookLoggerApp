using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// SEC-08: plant-tier purchase entitlement enforced in the service, not just the UI overlay.
/// SEC-11: hidden-by-entitlement plants must not surface in reads after a downgrade.
/// </summary>
public class PlantServiceEntitlementTests : IDisposable
{
    private readonly DbContextTestHelper _dbHelper;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AppSettingsProvider _settingsProvider;
    private readonly IMemoryCache _cache;
    private readonly IDecorationService _decorationService;

    public PlantServiceEntitlementTests()
    {
        _dbHelper = DbContextTestHelper.CreateTestContext();
        var services = new ServiceCollection();
        services.AddMemoryCache();
        _cache = services.BuildServiceProvider().GetRequiredService<IMemoryCache>();

        _unitOfWork = new UnitOfWork(_dbHelper.Context);
        _settingsProvider = new AppSettingsProvider(new TestDbContextFactory(_dbHelper.DatabaseName));
        _decorationService = Substitute.For<IDecorationService>();
        _decorationService.UserOwnsAbilityAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
    }

    public void Dispose() => _dbHelper.Dispose();

    private PlantService CreateService(SubscriptionTier tier) => new(
        _unitOfWork,
        _settingsProvider,
        _decorationService,
        _cache,
        NullLogger<PlantService>.Instance,
        analytics: null,
        featureGuard: new FeatureGuard(new FakeEntitlementService(tier)));

    private async Task<PlantSpecies> SeedSpeciesAsync(bool isFree, bool isPrestige)
    {
        var species = new PlantSpecies
        {
            Name = "Tier Species",
            ImagePath = "/x.svg",
            BaseCost = 100,
            UnlockLevel = 1,
            IsAvailable = true,
            IsFreeTier = isFree,
            IsPrestigeTier = isPrestige
        };
        var result = await _unitOfWork.PlantSpecies.AddAsync(species);
        await _dbHelper.Context.SaveChangesAsync();
        return result;
    }

    private async Task GiveCoinsAsync(int amount)
    {
        var settings = await _dbHelper.Context.AppSettings.FirstAsync();
        settings.Coins = amount;
        await _dbHelper.Context.SaveChangesAsync();
        _settingsProvider.InvalidateCache();
    }

    [Fact]
    public async Task PurchasePlantAsync_FreeUser_StandardPlant_ThrowsAndDoesNotSpendCoins()
    {
        var species = await SeedSpeciesAsync(isFree: false, isPrestige: false);
        await GiveCoinsAsync(10_000);
        var service = CreateService(SubscriptionTier.Free);

        Func<Task> act = () => service.PurchasePlantAsync(species.Id, "Mine");

        (await act.Should().ThrowAsync<EntitlementRequiredException>())
            .Which.Feature.Should().Be(FeatureKey.StandardPlantsAndDecorations);

        (await _settingsProvider.GetUserCoinsAsync()).Should().Be(10_000, "the guard must throw before any coins are spent");
        (await _unitOfWork.UserPlants.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task PurchasePlantAsync_PlusUser_PrestigePlant_ThrowsAndDoesNotSpendCoins()
    {
        var species = await SeedSpeciesAsync(isFree: false, isPrestige: true);
        await GiveCoinsAsync(10_000);
        var service = CreateService(SubscriptionTier.Plus);

        Func<Task> act = () => service.PurchasePlantAsync(species.Id, "Mine");

        (await act.Should().ThrowAsync<EntitlementRequiredException>())
            .Which.Feature.Should().Be(FeatureKey.PrestigePlants);

        (await _settingsProvider.GetUserCoinsAsync()).Should().Be(10_000, "the guard must throw before any coins are spent");
        (await _unitOfWork.UserPlants.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task PurchasePlantAsync_PremiumUser_PrestigePlant_Succeeds()
    {
        var species = await SeedSpeciesAsync(isFree: false, isPrestige: true);
        await GiveCoinsAsync(10_000);
        var service = CreateService(SubscriptionTier.Premium);

        var plant = await service.PurchasePlantAsync(species.Id, "Mine");

        plant.Should().NotBeNull();
        plant.SpeciesId.Should().Be(species.Id);
    }

    [Fact]
    public async Task PurchasePlantAsync_FreeUser_FreeTierPlant_Succeeds()
    {
        var species = await SeedSpeciesAsync(isFree: true, isPrestige: false);
        await GiveCoinsAsync(10_000);
        var service = CreateService(SubscriptionTier.Free);

        var plant = await service.PurchasePlantAsync(species.Id, "Mine");

        plant.Should().NotBeNull();
        plant.SpeciesId.Should().Be(species.Id);
    }

    [Fact]
    public async Task PurchasePlantAsync_PlusUser_StandardPlant_Succeeds()
    {
        var species = await SeedSpeciesAsync(isFree: false, isPrestige: false);
        await GiveCoinsAsync(10_000);
        var service = CreateService(SubscriptionTier.Plus);

        var plant = await service.PurchasePlantAsync(species.Id, "Mine");

        plant.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllAsync_ExcludesPlantsHiddenByEntitlement()
    {
        var species = await SeedSpeciesAsync(isFree: false, isPrestige: false);
        _dbHelper.Context.UserPlants.Add(new UserPlant
        {
            SpeciesId = species.Id, Name = "Visible", CurrentLevel = 1,
            PlantedAt = DateTime.UtcNow, LastWatered = DateTime.UtcNow, IsHiddenByEntitlement = false
        });
        _dbHelper.Context.UserPlants.Add(new UserPlant
        {
            SpeciesId = species.Id, Name = "HiddenPrestige", CurrentLevel = 1,
            PlantedAt = DateTime.UtcNow, LastWatered = DateTime.UtcNow, IsHiddenByEntitlement = true
        });
        await _dbHelper.Context.SaveChangesAsync();

        var service = CreateService(SubscriptionTier.Free);
        var plants = await service.GetAllAsync();

        plants.Should().ContainSingle().Which.Name.Should().Be("Visible");
    }

    [Fact]
    public async Task GetActivePlantAsync_IgnoresActivePlantHiddenByEntitlement()
    {
        var species = await SeedSpeciesAsync(isFree: false, isPrestige: false);
        _dbHelper.Context.UserPlants.Add(new UserPlant
        {
            SpeciesId = species.Id, Name = "HiddenActive", CurrentLevel = 1, IsActive = true,
            PlantedAt = DateTime.UtcNow, LastWatered = DateTime.UtcNow, IsHiddenByEntitlement = true
        });
        await _dbHelper.Context.SaveChangesAsync();

        var service = CreateService(SubscriptionTier.Free);
        var active = await service.GetActivePlantAsync();

        active.Should().BeNull("a hidden prestige plant must not be returned as the active plant after a downgrade");
    }
}
