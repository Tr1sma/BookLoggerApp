using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class DecorationShopViewModelTests
{
    private readonly IDecorationService _decorationService;
    private readonly IAppSettingsProvider _settings;
    private readonly DecorationShopViewModel _vm;

    public DecorationShopViewModelTests()
    {
        DatabaseInitializationHelper.MarkAsInitialized();
        _decorationService = Substitute.For<IDecorationService>();
        _settings = Substitute.For<IAppSettingsProvider>();

        _decorationService.GetAllDecorationShopItemsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ShopItem>>(new List<ShopItem>()));
        _settings.GetUserCoinsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(100));
        _settings.GetUserLevelAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(5));

        _vm = new DecorationShopViewModel(_decorationService, _settings);
    }

    [Fact]
    public async Task LoadAsync_LoadsDecorationsCoinsAndLevel()
    {
        var items = new List<ShopItem>
        {
            new() { Id = Guid.NewGuid(), Name = "Lamp", ItemType = ShopItemType.Decoration, Cost = 50, UnlockLevel = 1 },
            new() { Id = Guid.NewGuid(), Name = "Frame", ItemType = ShopItemType.Decoration, Cost = 100, UnlockLevel = 3 }
        };
        _decorationService.GetAllDecorationShopItemsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ShopItem>>(items));

        await _vm.LoadCommand.ExecuteAsync(null);

        _vm.AllDecorations.Should().HaveCount(2);
        _vm.UserCoins.Should().Be(100);
        _vm.UserLevel.Should().Be(5);
    }

    [Fact]
    public async Task PurchaseDecorationAsync_UnknownId_SetsError()
    {
        await _vm.LoadCommand.ExecuteAsync(null);

        await _vm.PurchaseDecorationCommand.ExecuteAsync(Guid.NewGuid());

        _vm.ErrorMessage.Should().NotBeNull();
        _vm.ErrorMessage!.Should().Contain("not found");
    }

    [Fact]
    public async Task PurchaseDecorationAsync_LevelTooLow_SetsError()
    {
        var id = Guid.NewGuid();
        var items = new List<ShopItem>
        {
            new() { Id = id, Name = "High", ItemType = ShopItemType.Decoration, Cost = 50, UnlockLevel = 99 }
        };
        _decorationService.GetAllDecorationShopItemsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ShopItem>>(items));

        await _vm.LoadCommand.ExecuteAsync(null);
        await _vm.PurchaseDecorationCommand.ExecuteAsync(id);

        _vm.ErrorMessage.Should().NotBeNull();
        _vm.ErrorMessage!.Should().Contain("level 99");
        await _decorationService.DidNotReceive().PurchaseDecorationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurchaseDecorationAsync_NotEnoughCoins_SetsError()
    {
        var id = Guid.NewGuid();
        var items = new List<ShopItem>
        {
            new() { Id = id, Name = "Expensive", ItemType = ShopItemType.Decoration, Cost = 500, UnlockLevel = 1 }
        };
        _decorationService.GetAllDecorationShopItemsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ShopItem>>(items));
        _settings.GetUserCoinsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(50));

        await _vm.LoadCommand.ExecuteAsync(null);
        await _vm.PurchaseDecorationCommand.ExecuteAsync(id);

        _vm.ErrorMessage.Should().NotBeNull();
        _vm.ErrorMessage!.Should().Contain("Not enough coins");
    }

    [Fact]
    public async Task PurchaseDecorationAsync_ValidPurchase_CallsService()
    {
        var id = Guid.NewGuid();
        var items = new List<ShopItem>
        {
            new() { Id = id, Name = "Nice", ItemType = ShopItemType.Decoration, Cost = 50, UnlockLevel = 1 }
        };
        _decorationService.GetAllDecorationShopItemsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ShopItem>>(items));

        await _vm.LoadCommand.ExecuteAsync(null);
        await _vm.PurchaseDecorationCommand.ExecuteAsync(id);

        await _decorationService.Received(1).PurchaseDecorationAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SelectDecoration_SetsSelectedAndClearsError()
    {
        var item = new ShopItem { Name = "X" };
        _vm.ErrorMessage = "some error";

        _vm.SelectDecorationCommand.Execute(item);

        _vm.SelectedDecoration.Should().Be(item);
        _vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ClearSelection_ClearsSelectedAndError()
    {
        _vm.SelectedDecoration = new ShopItem { Name = "X" };
        _vm.ErrorMessage = "some error";

        _vm.ClearSelectionCommand.Execute(null);

        _vm.SelectedDecoration.Should().BeNull();
        _vm.ErrorMessage.Should().BeNull();
    }
}
