using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class PlantShopViewModelTests
{
    private readonly IPlantService _plantService;
    private readonly IAppSettingsProvider _settings;
    private readonly PlantShopViewModel _vm;

    public PlantShopViewModelTests()
    {
        DatabaseInitializationHelper.MarkAsInitialized();
        _plantService = Substitute.For<IPlantService>();
        _settings = Substitute.For<IAppSettingsProvider>();

        _plantService.GetAllSpeciesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PlantSpecies>>(new List<PlantSpecies>()));
        _plantService.GetPlantCostAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(50));
        _settings.GetUserCoinsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(100));
        _settings.GetUserLevelAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(5));

        _vm = new PlantShopViewModel(_plantService, _settings);
    }

    [Fact]
    public async Task LoadAsync_LoadsCoinsLevelAndSpecies()
    {
        var species = new List<PlantSpecies>
        {
            new() { Id = Guid.NewGuid(), Name = "Rose", IsAvailable = true, UnlockLevel = 1, BaseCost = 50 },
            new() { Id = Guid.NewGuid(), Name = "Oak", IsAvailable = true, UnlockLevel = 5, BaseCost = 200 }
        };
        _plantService.GetAllSpeciesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PlantSpecies>>(species));

        await _vm.LoadCommand.ExecuteAsync(null);

        _vm.UserCoins.Should().Be(100);
        _vm.UserLevel.Should().Be(5);
        _vm.AvailableSpecies.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_FiltersOutUnavailableSpecies()
    {
        var species = new List<PlantSpecies>
        {
            new() { Id = Guid.NewGuid(), Name = "Available", IsAvailable = true, UnlockLevel = 1, BaseCost = 50 },
            new() { Id = Guid.NewGuid(), Name = "Hidden", IsAvailable = false, UnlockLevel = 1, BaseCost = 50 }
        };
        _plantService.GetAllSpeciesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PlantSpecies>>(species));

        await _vm.LoadCommand.ExecuteAsync(null);

        _vm.AvailableSpecies.Should().HaveCount(1);
        _vm.AvailableSpecies[0].Name.Should().Be("Available");
    }

    [Fact]
    public async Task PurchasePlantAsync_InsufficientCoins_SetsErrorMessage()
    {
        var speciesId = Guid.NewGuid();
        var species = new List<PlantSpecies>
        {
            new() { Id = speciesId, Name = "Rose", IsAvailable = true, UnlockLevel = 1, BaseCost = 500 }
        };
        _plantService.GetAllSpeciesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PlantSpecies>>(species));
        _plantService.GetPlantCostAsync(speciesId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(500));
        _settings.GetUserCoinsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(50));

        await _vm.LoadCommand.ExecuteAsync(null);
        await _vm.PurchasePlantCommand.ExecuteAsync(speciesId);

        _vm.ErrorMessage.Should().NotBeNull();
        _vm.ErrorMessage!.Should().Contain("Not enough coins");
        await _plantService.DidNotReceive().PurchasePlantAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurchasePlantAsync_LevelTooLow_SetsErrorMessage()
    {
        var speciesId = Guid.NewGuid();
        var species = new List<PlantSpecies>
        {
            new() { Id = speciesId, Name = "Oak", IsAvailable = true, UnlockLevel = 20, BaseCost = 100 }
        };
        _plantService.GetAllSpeciesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PlantSpecies>>(species));

        await _vm.LoadCommand.ExecuteAsync(null);
        await _vm.PurchasePlantCommand.ExecuteAsync(speciesId);

        _vm.ErrorMessage.Should().NotBeNull();
        _vm.ErrorMessage!.Should().Contain("level 20");
        await _plantService.DidNotReceive().PurchasePlantAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurchasePlantAsync_NotFoundSpecies_SetsErrorMessage()
    {
        var species = new List<PlantSpecies>
        {
            new() { Id = Guid.NewGuid(), Name = "Rose", IsAvailable = true, UnlockLevel = 1, BaseCost = 50 }
        };
        _plantService.GetAllSpeciesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PlantSpecies>>(species));

        await _vm.LoadCommand.ExecuteAsync(null);
        await _vm.PurchasePlantCommand.ExecuteAsync(Guid.NewGuid());

        _vm.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task PurchasePlantAsync_ValidPurchase_CallsService()
    {
        var speciesId = Guid.NewGuid();
        var species = new List<PlantSpecies>
        {
            new() { Id = speciesId, Name = "Rose", IsAvailable = true, UnlockLevel = 1, BaseCost = 50 }
        };
        _plantService.GetAllSpeciesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PlantSpecies>>(species));
        _plantService.GetPlantCostAsync(speciesId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(50));

        await _vm.LoadCommand.ExecuteAsync(null);
        _vm.NewPlantName = "My Rose";
        await _vm.PurchasePlantCommand.ExecuteAsync(speciesId);

        await _plantService.Received(1).PurchasePlantAsync(speciesId, "My Rose", Arg.Any<CancellationToken>());
        _vm.NewPlantName.Should().Be("");
    }

    [Fact]
    public async Task PurchasePlantAsync_EmptyName_UsesSpeciesName()
    {
        var speciesId = Guid.NewGuid();
        var species = new List<PlantSpecies>
        {
            new() { Id = speciesId, Name = "Rose", IsAvailable = true, UnlockLevel = 1, BaseCost = 50 }
        };
        _plantService.GetAllSpeciesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PlantSpecies>>(species));
        _plantService.GetPlantCostAsync(speciesId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(50));

        await _vm.LoadCommand.ExecuteAsync(null);
        _vm.NewPlantName = "";
        await _vm.PurchasePlantCommand.ExecuteAsync(speciesId);

        await _plantService.Received(1).PurchasePlantAsync(speciesId, "Rose", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GetDynamicPrice_UnknownId_ReturnsZero()
    {
        var result = _vm.GetDynamicPrice(Guid.NewGuid());

        result.Should().Be(0);
    }

    [Fact]
    public void SelectSpecies_SetsSelectedSpecies()
    {
        var sp = new PlantSpecies { Name = "Test" };

        _vm.SelectSpeciesCommand.Execute(sp);

        _vm.SelectedSpecies.Should().Be(sp);
    }

    [Fact]
    public void ClearSelection_ClearsSelectedAndName()
    {
        _vm.SelectedSpecies = new PlantSpecies { Name = "Test" };
        _vm.NewPlantName = "Foo";

        _vm.ClearSelectionCommand.Execute(null);

        _vm.SelectedSpecies.Should().BeNull();
        _vm.NewPlantName.Should().Be("");
    }
}
