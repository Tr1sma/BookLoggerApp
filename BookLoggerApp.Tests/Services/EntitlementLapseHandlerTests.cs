using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// Lapse/restore data-guard: hide overflow shelves, reduce to one active plant,
/// hide prestige/ultimate items, and clear those flags on re-upgrade.
/// </summary>
public class EntitlementLapseHandlerTests : IDisposable
{
    private readonly InMemoryFactory _factory;
    private readonly EntitlementLapseHandler _handler;

    public EntitlementLapseHandlerTests()
    {
        _factory = new InMemoryFactory(Guid.NewGuid().ToString());
        _handler = new EntitlementLapseHandler(_factory);
    }

    public void Dispose()
    {
        using AppDbContext ctx = _factory.CreateDbContext();
        ctx.Database.EnsureDeleted();
    }

    [Fact]
    public async Task ApplyLapseAsync_reduces_plants_to_one_active_and_keeps_oldest_healthy()
    {
        Guid species = await SeedSpeciesAsync(isPrestige: false, isFree: true);

        var oldest = await SeedPlantAsync(species, name: "First", plantedAt: new DateTime(2026, 1, 1), isActive: false, status: PlantStatus.Healthy);
        var middle = await SeedPlantAsync(species, name: "Second", plantedAt: new DateTime(2026, 2, 1), isActive: true, status: PlantStatus.Healthy);
        var newest = await SeedPlantAsync(species, name: "Third", plantedAt: new DateTime(2026, 3, 1), isActive: false, status: PlantStatus.Healthy);

        await _handler.ApplyLapseAsync();

        await using AppDbContext ctx = _factory.CreateDbContext();
        List<UserPlant> plants = await ctx.UserPlants.OrderBy(p => p.PlantedAt).ToListAsync();
        plants.Should().HaveCount(3);
        plants.Count(p => p.IsActive).Should().Be(1);
        // "middle" wins: oldest among active+healthy candidates ("oldest" was not active).
        plants.Single(p => p.IsActive).Name.Should().Be("Second");
        plants.Should().OnlyContain(p => p.IsHiddenByEntitlement == false,
            "free-tier plants should only lose IsActive, never get hidden");
    }

    [Fact]
    public async Task ApplyLapseAsync_falls_back_to_oldest_when_no_active_plant()
    {
        Guid species = await SeedSpeciesAsync(isPrestige: false, isFree: true);

        await SeedPlantAsync(species, name: "Third", plantedAt: new DateTime(2026, 3, 1), isActive: false, status: PlantStatus.Thirsty);
        await SeedPlantAsync(species, name: "First", plantedAt: new DateTime(2026, 1, 1), isActive: false, status: PlantStatus.Wilting);
        await SeedPlantAsync(species, name: "Second", plantedAt: new DateTime(2026, 2, 1), isActive: false, status: PlantStatus.Thirsty);

        await _handler.ApplyLapseAsync();

        await using AppDbContext ctx = _factory.CreateDbContext();
        List<UserPlant> plants = await ctx.UserPlants.ToListAsync();
        plants.Count(p => p.IsActive).Should().Be(1);
        plants.Single(p => p.IsActive).Name.Should().Be("First");
    }

    [Fact]
    public async Task ApplyLapseAsync_hides_prestige_plants_and_keeps_non_prestige_active()
    {
        Guid regular = await SeedSpeciesAsync(isPrestige: false, isFree: true);
        Guid prestige = await SeedSpeciesAsync(isPrestige: true);

        await SeedPlantAsync(prestige, name: "Phoenix", plantedAt: new DateTime(2026, 1, 1), isActive: true, status: PlantStatus.Healthy);
        await SeedPlantAsync(regular, name: "Fern", plantedAt: new DateTime(2026, 2, 1), isActive: false, status: PlantStatus.Healthy);

        await _handler.ApplyLapseAsync();

        await using AppDbContext ctx = _factory.CreateDbContext();
        List<UserPlant> plants = await ctx.UserPlants.ToListAsync();
        plants.Single(p => p.Name == "Phoenix").IsHiddenByEntitlement.Should().BeTrue();
        plants.Single(p => p.Name == "Phoenix").IsActive.Should().BeFalse();
        plants.Single(p => p.Name == "Fern").IsHiddenByEntitlement.Should().BeFalse();
        plants.Single(p => p.Name == "Fern").IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyLapseAsync_hides_shelves_beyond_cap_but_keeps_data()
    {
        await SeedShelfAsync("Shelf 1", sortOrder: 0);
        await SeedShelfAsync("Shelf 2", sortOrder: 1);
        await SeedShelfAsync("Shelf 3", sortOrder: 2);
        await SeedShelfAsync("Shelf 4", sortOrder: 3);
        await SeedShelfAsync("Shelf 5", sortOrder: 4);

        await _handler.ApplyLapseAsync();

        await using AppDbContext ctx = _factory.CreateDbContext();
        List<Shelf> shelves = await ctx.Shelves.OrderBy(s => s.SortOrder).ToListAsync();
        shelves.Should().HaveCount(5, "lapsed shelves must not be deleted");
        shelves.Where(s => !s.IsHiddenByEntitlement).Select(s => s.Name).Should().BeEquivalentTo(new[] { "Shelf 1", "Shelf 2", "Shelf 3" });
        shelves.Where(s => s.IsHiddenByEntitlement).Select(s => s.Name).Should().BeEquivalentTo(new[] { "Shelf 4", "Shelf 5" });
    }

    [Fact]
    public async Task ApplyLapseAsync_hides_ultimate_decoration()
    {
        Guid ultimate = await SeedShopItemAsync("Heart of Stories", isUltimate: true);
        Guid free = await SeedShopItemAsync("Reading Candle", isUltimate: false, isFree: true);

        await SeedUserDecorationAsync(ultimate);
        await SeedUserDecorationAsync(free);

        await _handler.ApplyLapseAsync();

        await using AppDbContext ctx = _factory.CreateDbContext();
        List<UserDecoration> decos = await ctx.UserDecorations.Include(d => d.ShopItem).ToListAsync();
        decos.Single(d => d.ShopItem.Name == "Heart of Stories").IsHiddenByEntitlement.Should().BeTrue();
        decos.Single(d => d.ShopItem.Name == "Reading Candle").IsHiddenByEntitlement.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyLapseAsync_hides_standard_tier_plants_but_keeps_free()
    {
        // HIGH-1003 / SEC-04: Free gets only free-tier plants. Standard (Plus) and prestige
        // (Premium) plants are hidden (not deleted) so a restored higher-tier backup can't leak them.
        Guid free = await SeedSpeciesAsync(isFree: true);
        Guid standard = await SeedSpeciesAsync(isPrestige: false, isFree: false);
        Guid prestige = await SeedSpeciesAsync(isPrestige: true);

        await SeedPlantAsync(free, name: "Free", plantedAt: new DateTime(2026, 1, 1), isActive: false, status: PlantStatus.Healthy);
        await SeedPlantAsync(standard, name: "Standard", plantedAt: new DateTime(2026, 2, 1), isActive: true, status: PlantStatus.Healthy);
        await SeedPlantAsync(prestige, name: "Prestige", plantedAt: new DateTime(2026, 3, 1), isActive: false, status: PlantStatus.Healthy);

        await _handler.ApplyLapseAsync();

        await using AppDbContext ctx = _factory.CreateDbContext();
        List<UserPlant> plants = await ctx.UserPlants.ToListAsync();
        plants.Single(p => p.Name == "Standard").IsHiddenByEntitlement.Should().BeTrue("standard plants require Plus");
        plants.Single(p => p.Name == "Prestige").IsHiddenByEntitlement.Should().BeTrue("prestige plants require Premium");
        plants.Single(p => p.Name == "Free").IsHiddenByEntitlement.Should().BeFalse("free-tier plants stay for Free users");
        plants.Single(p => p.Name == "Free").IsActive.Should().BeTrue("the one remaining free plant becomes active");
        plants.Count(p => p.IsActive).Should().Be(1);
    }

    [Fact]
    public async Task ApplyLapseAsync_hides_standard_tier_decorations_but_keeps_free()
    {
        // HIGH-1003 / SEC-04: standard (Plus) and ultimate (Premium) decorations hidden for Free.
        Guid free = await SeedShopItemAsync("Starter Lamp", isUltimate: false, isFree: true);
        Guid standard = await SeedShopItemAsync("Cozy Rug", isUltimate: false, isFree: false);
        Guid ultimate = await SeedShopItemAsync("Heart of Stories", isUltimate: true);

        await SeedUserDecorationAsync(free);
        await SeedUserDecorationAsync(standard);
        await SeedUserDecorationAsync(ultimate);

        await _handler.ApplyLapseAsync();

        await using AppDbContext ctx = _factory.CreateDbContext();
        List<UserDecoration> decos = await ctx.UserDecorations.Include(d => d.ShopItem).ToListAsync();
        decos.Single(d => d.ShopItem.Name == "Cozy Rug").IsHiddenByEntitlement.Should().BeTrue("standard decorations require Plus");
        decos.Single(d => d.ShopItem.Name == "Heart of Stories").IsHiddenByEntitlement.Should().BeTrue("ultimate decorations require Premium");
        decos.Single(d => d.ShopItem.Name == "Starter Lamp").IsHiddenByEntitlement.Should().BeFalse("free decorations stay for Free users");
    }

    [Fact]
    public async Task ClearEntitlementHidesAsync_Premium_unhides_everything()
    {
        Guid species = await SeedSpeciesAsync(isPrestige: true);
        var plant = await SeedPlantAsync(species, name: "Phoenix", plantedAt: DateTime.UtcNow, isActive: false, status: PlantStatus.Healthy);

        Guid ultimate = await SeedShopItemAsync("Heart of Stories", isUltimate: true);
        var deco = await SeedUserDecorationAsync(ultimate);

        await using (var setup = _factory.CreateDbContext())
        {
            plant = await setup.UserPlants.FirstAsync();
            plant.IsHiddenByEntitlement = true;
            deco = await setup.UserDecorations.FirstAsync();
            deco.IsHiddenByEntitlement = true;
            await setup.SaveChangesAsync();
        }

        await SeedShelfAsync("Shelf A", sortOrder: 0);
        await SeedShelfAsync("Shelf B", sortOrder: 1);
        await using (var setup = _factory.CreateDbContext())
        {
            foreach (var s in await setup.Shelves.ToListAsync())
            {
                s.IsHiddenByEntitlement = true;
            }
            await setup.SaveChangesAsync();
        }

        await _handler.ClearEntitlementHidesAsync(SubscriptionTier.Premium);

        await using AppDbContext ctx = _factory.CreateDbContext();
        (await ctx.UserPlants.AllAsync(p => !p.IsHiddenByEntitlement)).Should().BeTrue();
        (await ctx.Shelves.AllAsync(s => !s.IsHiddenByEntitlement)).Should().BeTrue();
        (await ctx.UserDecorations.AllAsync(d => !d.IsHiddenByEntitlement)).Should().BeTrue();
    }

    [Fact]
    public async Task ClearEntitlementHidesAsync_Plus_unhides_standard_but_keeps_prestige_and_ultimate_hidden()
    {
        // SEC-04: granting Plus must not un-hide Premium-only content (prestige plants,
        // ultimate decorations). Standard plants, shelves and standard decorations come back.
        Guid regular = await SeedSpeciesAsync(isPrestige: false);
        Guid prestige = await SeedSpeciesAsync(isPrestige: true);
        await SeedPlantAsync(regular, name: "Fern", plantedAt: new DateTime(2026, 1, 1), isActive: false, status: PlantStatus.Healthy);
        await SeedPlantAsync(prestige, name: "Phoenix", plantedAt: new DateTime(2026, 2, 1), isActive: false, status: PlantStatus.Healthy);

        Guid ultimate = await SeedShopItemAsync("Heart of Stories", isUltimate: true);
        Guid standard = await SeedShopItemAsync("Reading Candle", isUltimate: false);
        await SeedUserDecorationAsync(ultimate);
        await SeedUserDecorationAsync(standard);

        await SeedShelfAsync("Shelf A", sortOrder: 0);

        // Hide everything first (Free state).
        await using (var setup = _factory.CreateDbContext())
        {
            foreach (var p in await setup.UserPlants.ToListAsync()) p.IsHiddenByEntitlement = true;
            foreach (var d in await setup.UserDecorations.ToListAsync()) d.IsHiddenByEntitlement = true;
            foreach (var s in await setup.Shelves.ToListAsync()) s.IsHiddenByEntitlement = true;
            await setup.SaveChangesAsync();
        }

        await _handler.ClearEntitlementHidesAsync(SubscriptionTier.Plus);

        await using AppDbContext ctx = _factory.CreateDbContext();
        List<UserPlant> plants = await ctx.UserPlants.Include(p => p.Species).ToListAsync();
        plants.Single(p => p.Name == "Fern").IsHiddenByEntitlement.Should().BeFalse("standard plants unlock at Plus");
        plants.Single(p => p.Name == "Phoenix").IsHiddenByEntitlement.Should().BeTrue("prestige plants require Premium");

        List<UserDecoration> decos = await ctx.UserDecorations.Include(d => d.ShopItem).ToListAsync();
        decos.Single(d => d.ShopItem.Name == "Reading Candle").IsHiddenByEntitlement.Should().BeFalse("standard decorations unlock at Plus");
        decos.Single(d => d.ShopItem.Name == "Heart of Stories").IsHiddenByEntitlement.Should().BeTrue("ultimate decorations require Premium");

        (await ctx.Shelves.AllAsync(s => !s.IsHiddenByEntitlement)).Should().BeTrue("Plus unlocks unlimited shelves");
    }

    [Fact]
    public async Task ClearEntitlementHidesAsync_Plus_rehides_currently_visible_premium_content()
    {
        // Premium→Plus downgrade: prestige plant + ultimate decoration were visible under
        // Premium and must be re-hidden when the new tier is only Plus.
        Guid regular = await SeedSpeciesAsync(isPrestige: false);
        Guid prestige = await SeedSpeciesAsync(isPrestige: true);
        await SeedPlantAsync(regular, name: "Fern", plantedAt: new DateTime(2026, 1, 1), isActive: true, status: PlantStatus.Healthy);
        await SeedPlantAsync(prestige, name: "Phoenix", plantedAt: new DateTime(2026, 2, 1), isActive: true, status: PlantStatus.Healthy);

        Guid ultimate = await SeedShopItemAsync("Heart of Stories", isUltimate: true);
        await SeedUserDecorationAsync(ultimate);
        // Everything starts visible (IsHiddenByEntitlement == false by default).

        await _handler.ClearEntitlementHidesAsync(SubscriptionTier.Plus);

        await using AppDbContext ctx = _factory.CreateDbContext();
        UserPlant phoenix = await ctx.UserPlants.Include(p => p.Species).SingleAsync(p => p.Name == "Phoenix");
        phoenix.IsHiddenByEntitlement.Should().BeTrue("prestige plant must be re-hidden on downgrade to Plus");
        phoenix.IsActive.Should().BeFalse("a hidden prestige plant cannot stay active");

        UserDecoration heart = await ctx.UserDecorations.Include(d => d.ShopItem).SingleAsync(d => d.ShopItem.Name == "Heart of Stories");
        heart.IsHiddenByEntitlement.Should().BeTrue("ultimate decoration must be re-hidden on downgrade to Plus");

        ctx.UserPlants.Single(p => p.Name == "Fern").IsActive.Should().BeTrue("a standard plant stays active under Plus");
    }

    private async Task<Guid> SeedSpeciesAsync(bool isPrestige = false, bool isFree = false)
    {
        await using AppDbContext ctx = _factory.CreateDbContext();
        PlantSpecies species = new()
        {
            Id = Guid.NewGuid(),
            Name = isPrestige ? $"Prestige_{Guid.NewGuid():N}" : $"Regular_{Guid.NewGuid():N}",
            ImagePath = "dummy.svg",
            IsPrestigeTier = isPrestige,
            IsFreeTier = isFree
        };
        ctx.PlantSpecies.Add(species);
        await ctx.SaveChangesAsync();
        return species.Id;
    }

    private async Task<UserPlant> SeedPlantAsync(Guid speciesId, string name, DateTime plantedAt, bool isActive, PlantStatus status)
    {
        await using AppDbContext ctx = _factory.CreateDbContext();
        UserPlant plant = new()
        {
            Id = Guid.NewGuid(),
            SpeciesId = speciesId,
            Name = name,
            PlantedAt = plantedAt,
            LastWatered = plantedAt,
            IsActive = isActive,
            Status = status
        };
        ctx.UserPlants.Add(plant);
        await ctx.SaveChangesAsync();
        return plant;
    }

    private async Task<Shelf> SeedShelfAsync(string name, int sortOrder)
    {
        await using AppDbContext ctx = _factory.CreateDbContext();
        Shelf shelf = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            SortOrder = sortOrder
        };
        ctx.Shelves.Add(shelf);
        await ctx.SaveChangesAsync();
        return shelf;
    }

    private async Task<Guid> SeedShopItemAsync(string name, bool isUltimate = false, bool isFree = false)
    {
        await using AppDbContext ctx = _factory.CreateDbContext();
        ShopItem item = new()
        {
            Id = Guid.NewGuid(),
            ItemType = ShopItemType.Decoration,
            Name = name,
            Cost = 100,
            ImagePath = "dummy.svg",
            UnlockLevel = 1,
            SlotWidth = 1,
            IsUltimateTier = isUltimate,
            IsFreeTier = isFree
        };
        ctx.ShopItems.Add(item);
        await ctx.SaveChangesAsync();
        return item.Id;
    }

    private async Task<UserDecoration> SeedUserDecorationAsync(Guid shopItemId)
    {
        await using AppDbContext ctx = _factory.CreateDbContext();
        UserDecoration deco = new()
        {
            Id = Guid.NewGuid(),
            ShopItemId = shopItemId,
            Name = "Owned",
            PurchasedAt = DateTime.UtcNow
        };
        ctx.UserDecorations.Add(deco);
        await ctx.SaveChangesAsync();
        return deco;
    }

    private sealed class InMemoryFactory : IDbContextFactory<AppDbContext>
    {
        private readonly string _dbName;
        public InMemoryFactory(string dbName) => _dbName = dbName;

        public AppDbContext CreateDbContext()
        {
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;
            AppDbContext ctx = new(options);
            ctx.Database.EnsureCreated();
            return ctx;
        }
    }
}
