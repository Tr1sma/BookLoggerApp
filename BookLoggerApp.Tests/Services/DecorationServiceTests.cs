using FluentAssertions;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class DecorationServiceTests : IDisposable
{
    private readonly DecorationService _decorationService;
    private readonly DbContextTestHelper _dbHelper;
    private readonly TestDbContextFactory _contextFactory;
    private readonly AppSettingsProvider _settingsProvider;

    public DecorationServiceTests()
    {
        _dbHelper = DbContextTestHelper.CreateTestContext();
        _contextFactory = new TestDbContextFactory(_dbHelper.DatabaseName);
        _settingsProvider = new AppSettingsProvider(_contextFactory);
        var logger = NullLogger<DecorationService>.Instance;
        _decorationService = new DecorationService(_contextFactory, _settingsProvider, logger);
    }

    public void Dispose()
    {
        _dbHelper.Dispose();
    }

    #region Purchase Tests

    [Fact]
    public async Task PurchaseDecorationAsync_WithSufficientCoins_CreatesUserDecoration()
    {
        // Arrange
        var shopItem = await SeedDecorationShopItem("Test Candle", 100, unlockLevel: 1);
        await GiveCoins(500);

        // Act
        var result = await _decorationService.PurchaseDecorationAsync(shopItem.Id);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Candle");
        result.ShopItemId.Should().Be(shopItem.Id);
        result.PurchasedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PurchaseDecorationAsync_WithInsufficientCoins_ThrowsInsufficientFundsException()
    {
        // Arrange
        var shopItem = await SeedDecorationShopItem("Expensive Deco", 9999, unlockLevel: 1);
        await GiveCoins(10);

        // Act
        var act = () => _decorationService.PurchaseDecorationAsync(shopItem.Id);

        // Assert
        await act.Should().ThrowAsync<InsufficientFundsException>();
    }

    [Fact]
    public async Task PurchaseDecorationAsync_UsesStaticCost()
    {
        // Arrange
        var shopItem = await SeedDecorationShopItem("Static Price Deco", 200, unlockLevel: 1);
        await GiveCoins(1000);

        // Act — buy the same decoration twice
        await _decorationService.PurchaseDecorationAsync(shopItem.Id);
        await _decorationService.PurchaseDecorationAsync(shopItem.Id);

        // Assert — coins deducted should be 2 × 200 = 400 (no multiplier)
        var coins = await _settingsProvider.GetUserCoinsAsync();
        coins.Should().Be(600); // 1000 - 200 - 200
    }

    [Fact]
    public async Task PurchaseDecorationAsync_WithNonDecorationItem_ThrowsInvalidOperationException()
    {
        // Arrange — create a Plant-type ShopItem
        var shopItem = new ShopItem
        {
            Id = Guid.NewGuid(),
            ItemType = ShopItemType.Plant,
            Name = "Not a decoration",
            Cost = 100,
            ImagePath = "images/test.svg",
            IsAvailable = true,
            UnlockLevel = 1
        };
        _dbHelper.Context.ShopItems.Add(shopItem);
        await _dbHelper.Context.SaveChangesAsync();
        await GiveCoins(500);

        // Act
        var act = () => _decorationService.PurchaseDecorationAsync(shopItem.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not a decoration*");
    }

    #endregion

    #region GetAll Tests

    [Fact]
    public async Task GetAllDecorationShopItemsAsync_ReturnsOnlyDecorations()
    {
        // Arrange — add a Plant-type item (should NOT be returned)
        _dbHelper.Context.ShopItems.Add(new ShopItem
        {
            Id = Guid.NewGuid(),
            ItemType = ShopItemType.Plant,
            Name = "Not a decoration",
            Cost = 500,
            ImagePath = "images/test.svg",
            IsAvailable = true,
            UnlockLevel = 1
        });
        await _dbHelper.Context.SaveChangesAsync();

        // Act
        var result = await _decorationService.GetAllDecorationShopItemsAsync();

        // Assert — seed data provides 14 decorations; the Plant item must be excluded
        result.Should().HaveCountGreaterThanOrEqualTo(14);
        result.Should().OnlyContain(si => si.ItemType == ShopItemType.Decoration);
    }

    [Fact]
    public async Task GetAllDecorationShopItemsAsync_OrderedByUnlockLevelThenCost()
    {
        // Act — uses seed data (14 decorations ordered by UnlockLevel then Cost)
        var result = await _decorationService.GetAllDecorationShopItemsAsync();

        // Assert — verify ordering: each item's UnlockLevel should be >= previous
        for (int i = 1; i < result.Count; i++)
        {
            result[i].UnlockLevel.Should().BeGreaterThanOrEqualTo(result[i - 1].UnlockLevel);

            if (result[i].UnlockLevel == result[i - 1].UnlockLevel)
            {
                result[i].Cost.Should().BeGreaterThanOrEqualTo(result[i - 1].Cost);
            }
        }
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteAsync_RemovesDecorationAndShelfPlacements()
    {
        // Arrange
        var shopItem = await SeedDecorationShopItem("To Delete", 100, unlockLevel: 1);
        await GiveCoins(500);

        var deco = await _decorationService.PurchaseDecorationAsync(shopItem.Id);

        // Place it on a shelf
        var shelf = new Shelf { Id = Guid.NewGuid(), Name = "Test Shelf" };
        _dbHelper.Context.Shelves.Add(shelf);
        _dbHelper.Context.DecorationShelves.Add(new DecorationShelf
        {
            DecorationId = deco.Id,
            ShelfId = shelf.Id,
            Position = 0
        });
        await _dbHelper.Context.SaveChangesAsync();

        // Act
        await _decorationService.DeleteAsync(deco.Id);

        // Assert
        var allDecos = await _decorationService.GetAllAsync();
        allDecos.Should().BeEmpty();

        // Shelf placement should also be gone
        _dbHelper.Context.DecorationShelves.Should().BeEmpty();
    }

    #endregion

    #region Helpers

    private async Task<ShopItem> SeedDecorationShopItem(string name, int cost, int unlockLevel)
    {
        var shopItem = new ShopItem
        {
            Id = Guid.NewGuid(),
            ItemType = ShopItemType.Decoration,
            Name = name,
            Description = $"Test {name}",
            Cost = cost,
            ImagePath = $"images/decorations/{name.ToLower().Replace(' ', '_')}.svg",
            IsAvailable = true,
            UnlockLevel = unlockLevel
        };

        _dbHelper.Context.ShopItems.Add(shopItem);
        await _dbHelper.Context.SaveChangesAsync();
        return shopItem;
    }

    private async Task GiveCoins(int amount)
    {
        var settings = await _dbHelper.Context.AppSettings.FirstAsync();
        settings.Coins = amount;
        await _dbHelper.Context.SaveChangesAsync();
        _settingsProvider.InvalidateCache();
    }

    #endregion
}
