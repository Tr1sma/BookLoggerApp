using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// Covers the lapse / restore data-guard: hiding overflow shelves, reducing
/// plants to one active, hiding prestige/ultimate items, and clearing those
/// flags on re-upgrade.
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
        Guid species = await SeedSpeciesAsync(isPrestige: false);

        var oldest = await SeedPlantAsync(species, name: "First", plantedAt: new DateTime(2026, 1, 1), isActive: false, status: PlantStatus.Healthy);
        var middle = await SeedPlantAsync(species, name: "Second", plantedAt: new DateTime(2026, 2, 1), isActive: true, status: PlantStatus.Healthy);
        var newest = await SeedPlantAsync(species, name: "Third", plantedAt: new DateTime(2026, 3, 1), isActive: false, status: PlantStatus.Healthy);

        await _handler.ApplyLapseAsync();

        await using AppDbContext ctx = _factory.CreateDbContext();
        List<UserPlant> plants = await ctx.UserPlants.OrderBy(p => p.PlantedAt).ToListAsync();
        plants.Should().HaveCount(3);
        plants.Count(p => p.IsActive).Should().Be(1);
        // "middle" was both IsActive and Healthy; it wins (oldest among active+healthy candidates
        // is "middle" because "oldest" was not active).
        plants.Single(p => p.IsActive).Name.Should().Be("Second");
        plants.Should().OnlyContain(p => p.IsHiddenByEntitlement == false,
            "non-prestige plants should only lose IsActive, never get hidden");
    }

    [Fact]
    public async Task ApplyLapseAsync_falls_back_to_oldest_when_no_active_plant()
    {
        Guid species = await SeedSpeciesAsync(isPrestige: false);

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
        Guid regular = await SeedSpeciesAsync(isPrestige: false);
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
        Guid regular = await SeedShopItemAsync("Reading Candle", isUltimate: false);

        await SeedUserDecorationAsync(ultimate);
        await SeedUserDecorationAsync(regular);

        await _handler.ApplyLapseAsync();

        await using AppDbContext ctx = _factory.CreateDbContext();
        List<UserDecoration> decos = await ctx.UserDecorations.Include(d => d.ShopItem).ToListAsync();
        decos.Single(d => d.ShopItem.Name == "Heart of Stories").IsHiddenByEntitlement.Should().BeTrue();
        decos.Single(d => d.ShopItem.Name == "Reading Candle").IsHiddenByEntitlement.Should().BeFalse();
    }

    [Fact]
    public async Task ClearEntitlementHidesAsync_unhides_everything()
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

        await _handler.ClearEntitlementHidesAsync();

        await using AppDbContext ctx = _factory.CreateDbContext();
        (await ctx.UserPlants.AllAsync(p => !p.IsHiddenByEntitlement)).Should().BeTrue();
        (await ctx.Shelves.AllAsync(s => !s.IsHiddenByEntitlement)).Should().BeTrue();
        (await ctx.UserDecorations.AllAsync(d => !d.IsHiddenByEntitlement)).Should().BeTrue();
    }

    // ───── Seed helpers ───────────────────────────────────────────────────

    private async Task<Guid> SeedSpeciesAsync(bool isPrestige)
    {
        await using AppDbContext ctx = _factory.CreateDbContext();
        PlantSpecies species = new()
        {
            Id = Guid.NewGuid(),
            Name = isPrestige ? $"Prestige_{Guid.NewGuid():N}" : $"Regular_{Guid.NewGuid():N}",
            ImagePath = "dummy.svg",
            IsPrestigeTier = isPrestige
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

    private async Task<Guid> SeedShopItemAsync(string name, bool isUltimate)
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
            IsUltimateTier = isUltimate
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
