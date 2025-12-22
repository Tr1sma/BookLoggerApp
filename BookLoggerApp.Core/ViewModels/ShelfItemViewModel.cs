using CommunityToolkit.Mvvm.ComponentModel;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.ViewModels;

public partial class ShelfItemViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _itemId;

    [ObservableProperty]
    private ShelfItemType _type;

    [ObservableProperty]
    private int _position;

    // References
    public Book? Book { get; set; }
    public UserPlant? Plant { get; set; }

    // Helper for drag and drop identification
    public string DragId => $"{Type}_{ItemId}";

    public ShelfItemViewModel(ShelfItemDto dto)
    {
        ItemId = dto.ItemId;
        Type = dto.Type;
        Position = dto.Position;
        Book = dto.Book;
        Plant = dto.Plant;
    }
}
