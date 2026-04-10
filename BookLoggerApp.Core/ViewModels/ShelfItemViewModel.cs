using CommunityToolkit.Mvvm.ComponentModel;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.ViewModels;

public partial class ShelfItemViewModel : ObservableObject
{
    public Guid Id { get; set; }
    public int Position { get; set; }
    public ShelfItemType Type { get; set; }
    public int SlotWidth { get; set; } = 1;

    // Union-ish properties
    public Book? Book { get; set; }
    public UserPlant? Plant { get; set; }
    public UserDecoration? Decoration { get; set; }

    public ShelfItemViewModel(Book book, int position, int slotWidth = 1)
    {
        Id = book.Id;
        Type = ShelfItemType.Book;
        Book = book;
        Position = position;
        SlotWidth = slotWidth;
    }

    public ShelfItemViewModel(UserPlant plant, int position, int slotWidth = 1)
    {
        Id = plant.Id;
        Type = ShelfItemType.Plant;
        Plant = plant;
        Position = position;
        SlotWidth = slotWidth;
    }

    public ShelfItemViewModel(UserDecoration decoration, int position, int slotWidth = 1)
    {
        Id = decoration.Id;
        Type = ShelfItemType.Decoration;
        Decoration = decoration;
        Position = position;
        SlotWidth = slotWidth;
    }
}

public enum ShelfItemType
{
    Book,
    Plant,
    Decoration
}
