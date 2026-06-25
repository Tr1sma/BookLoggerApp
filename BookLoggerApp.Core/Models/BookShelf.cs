using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Join entity associating Books with Shelves. Explicitly managed to allow per-shelf
/// ordering; currently mainly the many-to-many link.
/// </summary>
public class BookShelf
{
    public Guid BookId { get; set; }
    [ForeignKey("BookId")]
    public Book Book { get; set; } = null!;

    public Guid ShelfId { get; set; }
    [ForeignKey("ShelfId")]
    public Shelf Shelf { get; set; } = null!;

    /// <summary>Position of the book on this shelf.</summary>
    public int Position { get; set; } = 0;
}
