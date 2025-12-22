using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents an item on a shelf (Book or Plant).
/// </summary>
public class ShelfItemDto
{
    public Guid ItemId { get; set; }
    public ShelfItemType Type { get; set; }
    public int Position { get; set; }

    // The actual entity references
    public Book? Book { get; set; }
    public UserPlant? Plant { get; set; }
}
