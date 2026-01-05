using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Join entity for associating Plants with Shelves.
/// Allows positioning plants on specific shelves.
/// </summary>
public class PlantShelf
{
    public Guid PlantId { get; set; }
    [ForeignKey("PlantId")]
    public UserPlant Plant { get; set; } = null!;

    public Guid ShelfId { get; set; }
    [ForeignKey("ShelfId")]
    public Shelf Shelf { get; set; } = null!;

    /// <summary>
    /// Position of the plant on this specific shelf.
    /// Can be interleaved with Books (both share the same coordinate system 0..N).
    /// </summary>
    public int Position { get; set; } = 0;
}
