using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoggerApp.Core.Models;

/// <summary>Join entity associating Plants with Shelves, with per-shelf positioning.</summary>
public class PlantShelf
{
    public Guid PlantId { get; set; }
    [ForeignKey("PlantId")]
    public UserPlant Plant { get; set; } = null!;

    public Guid ShelfId { get; set; }
    [ForeignKey("ShelfId")]
    public Shelf Shelf { get; set; } = null!;

    /// <summary>
    /// Position of the plant on this shelf. Can be interleaved with Books (shared 0..N coordinate system).
    /// </summary>
    public int Position { get; set; } = 0;
}
