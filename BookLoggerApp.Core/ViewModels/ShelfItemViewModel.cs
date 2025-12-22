using CommunityToolkit.Mvvm.ComponentModel;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.ViewModels;

public partial class ShelfItemViewModel : ObservableObject
{
    public Guid Id { get; set; }
    public int Position { get; set; }
    public ShelfItemType Type { get; set; }

    // Union-ish properties
    public Book? Book { get; set; }
    public UserPlant? Plant { get; set; }

    public ShelfItemViewModel(Book book, int position)
    {
        Id = book.Id;
        Type = ShelfItemType.Book;
        Book = book;
        Position = position;
    }

    public ShelfItemViewModel(UserPlant plant, int position)
    {
        Id = plant.Id;
        Type = ShelfItemType.Plant;
        Plant = plant;
        Position = position;
    }
}

public enum ShelfItemType
{
    Book,
    Plant
}
