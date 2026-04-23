using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class BookshelfViewModelTests
{
    private readonly IBookService _bookService;
    private readonly IGenreService _genreService;
    private readonly IPlantService _plantService;
    private readonly IGoalService _goalService;
    private readonly IDecorationService _decorationService;
    private readonly IShelfService _shelfService;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly BookshelfViewModel _viewModel;
    private readonly Shelf _mainShelf;

    public BookshelfViewModelTests()
    {
        BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.MarkAsInitialized();

        _bookService = Substitute.For<IBookService>();
        _genreService = Substitute.For<IGenreService>();
        _plantService = Substitute.For<IPlantService>();
        _goalService = Substitute.For<IGoalService>();
        _decorationService = Substitute.For<IDecorationService>();
        _shelfService = Substitute.For<IShelfService>();
        _settingsProvider = Substitute.For<IAppSettingsProvider>();

        _viewModel = new BookshelfViewModel(
            _bookService,
            _genreService,
            _plantService,
            _goalService,
            _decorationService,
            _shelfService,
            _settingsProvider
        );

        _mainShelf = new Shelf
        {
            Id = Guid.NewGuid(),
            Name = "Main Shelf",
            SortOrder = 0
        };

        ConfigureLoadDependencies();
    }

    [Fact]
    public async Task RenamePlantAsync_ShouldTrimName_UpdatePlant_AndReloadShelves()
    {
        // Arrange
        var plantId = Guid.NewGuid();
        var plant = new UserPlant
        {
            Id = plantId,
            Name = "Old Name",
            SpeciesId = Guid.NewGuid()
        };

        _plantService.GetByIdAsync(plantId, Arg.Any<CancellationToken>()).Returns(plant);

        // Act
        await _viewModel.RenamePlantAsync((plantId, "  New Name  "));

        // Assert
        plant.Name.Should().Be("New Name");
        _viewModel.ErrorMessage.Should().BeNull();
        await _plantService.Received(1).UpdateAsync(
            Arg.Is<UserPlant>(p => p.Id == plantId && p.Name == "New Name"),
            Arg.Any<CancellationToken>());
        await _shelfService.Received(1).GetAllShelvesAsync();
    }

    [Fact]
    public async Task RenamePlantAsync_ShouldSetError_WhenNameIsEmpty()
    {
        // Act
        await _viewModel.RenamePlantAsync((Guid.NewGuid(), "   "));

        // Assert
        _viewModel.ErrorMessage.Should().Be("PlantDetail_Error_NameEmpty");
        await _plantService.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task RenamePlantAsync_ShouldSkipUpdate_WhenNameIsUnchanged()
    {
        // Arrange
        var plantId = Guid.NewGuid();
        var plant = new UserPlant
        {
            Id = plantId,
            Name = "Story Seedling",
            SpeciesId = Guid.NewGuid()
        };

        _plantService.GetByIdAsync(plantId, Arg.Any<CancellationToken>()).Returns(plant);

        // Act
        await _viewModel.RenamePlantAsync((plantId, "Story Seedling"));

        // Assert
        _viewModel.ErrorMessage.Should().BeNull();
        await _plantService.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await _shelfService.DidNotReceive().GetAllShelvesAsync();
    }

    [Fact]
    public async Task LoadAsync_ShouldExcludeDeadPlantsFromAvailablePlants()
    {
        // Arrange
        var shelfPlant = new UserPlant
        {
            Id = Guid.NewGuid(),
            Name = "Shelf Plant",
            SpeciesId = Guid.NewGuid(),
            Status = PlantStatus.Healthy
        };
        var availablePlant = new UserPlant
        {
            Id = Guid.NewGuid(),
            Name = "Available Plant",
            SpeciesId = Guid.NewGuid(),
            Status = PlantStatus.Healthy
        };
        var deadPlant = new UserPlant
        {
            Id = Guid.NewGuid(),
            Name = "Dead Plant",
            SpeciesId = Guid.NewGuid(),
            Status = PlantStatus.Dead
        };

        _plantService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { shelfPlant, availablePlant, deadPlant });
        _shelfService.GetShelfByIdAsync(_mainShelf.Id).Returns(new Shelf
        {
            Id = _mainShelf.Id,
            Name = _mainShelf.Name,
            SortOrder = _mainShelf.SortOrder,
            BookShelves = new List<BookShelf>(),
            PlantShelves = new List<PlantShelf>
            {
                new()
                {
                    PlantId = shelfPlant.Id,
                    ShelfId = _mainShelf.Id,
                    Plant = shelfPlant,
                    Position = 0
                }
            }
        });

        // Act
        await _viewModel.LoadAsync();

        // Assert
        _viewModel.AvailablePlants.Should().ContainSingle();
        _viewModel.AvailablePlants[0].Id.Should().Be(availablePlant.Id);
    }

    private void ConfigureLoadDependencies()
    {
        _settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(new AppSettings());
        _bookService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Book>());
        _plantService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<UserPlant>());
        _decorationService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<UserDecoration>() as IReadOnlyList<UserDecoration>);
        _goalService.GetActiveGoalsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<ReadingGoal>());
        _shelfService.GetAllShelvesAsync().Returns(new List<Shelf> { _mainShelf });
        _shelfService.GetShelfByIdAsync(_mainShelf.Id).Returns(new Shelf
        {
            Id = _mainShelf.Id,
            Name = _mainShelf.Name,
            SortOrder = _mainShelf.SortOrder,
            BookShelves = new List<BookShelf>(),
            PlantShelves = new List<PlantShelf>(),
            DecorationShelves = new List<DecorationShelf>()
        });
    }
}
