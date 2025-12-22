using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Join entity for associating UserPlants with Shelves.
/// Allows positioning plants on shelves mixed with books.
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
    /// </summary>
    public int Position { get; set; } = 0;
}
