using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class ShelfItemViewModelTests
{
    [Fact]
    public void Constructor_WithBook_SetsTypeAndId()
    {
        var book = new Book { Id = Guid.NewGuid(), Title = "T", Author = "A" };

        var vm = new ShelfItemViewModel(book, position: 3, slotWidth: 2);

        vm.Id.Should().Be(book.Id);
        vm.Position.Should().Be(3);
        vm.SlotWidth.Should().Be(2);
        vm.Type.Should().Be(ShelfItemType.Book);
        vm.Book.Should().Be(book);
        vm.Plant.Should().BeNull();
        vm.Decoration.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithBook_DefaultSlotWidth_IsOne()
    {
        var book = new Book { Id = Guid.NewGuid(), Title = "T", Author = "A" };

        var vm = new ShelfItemViewModel(book, position: 0);

        vm.SlotWidth.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithPlant_SetsTypePlant()
    {
        var plant = new UserPlant { Id = Guid.NewGuid() };

        var vm = new ShelfItemViewModel(plant, position: 5);

        vm.Id.Should().Be(plant.Id);
        vm.Type.Should().Be(ShelfItemType.Plant);
        vm.Plant.Should().Be(plant);
        vm.Book.Should().BeNull();
        vm.Decoration.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithDecoration_SetsTypeDecoration()
    {
        var decoration = new UserDecoration { Id = Guid.NewGuid() };

        var vm = new ShelfItemViewModel(decoration, position: 7, slotWidth: 3);

        vm.Id.Should().Be(decoration.Id);
        vm.Position.Should().Be(7);
        vm.SlotWidth.Should().Be(3);
        vm.Type.Should().Be(ShelfItemType.Decoration);
        vm.Decoration.Should().Be(decoration);
        vm.Book.Should().BeNull();
        vm.Plant.Should().BeNull();
    }
}
