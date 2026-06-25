using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoggerApp.Core.Models;

/// <summary>Join entity associating UserDecorations with Shelves. Mirrors PlantShelf.</summary>
public class DecorationShelf
{
    public Guid DecorationId { get; set; }

    [ForeignKey("DecorationId")]
    public UserDecoration Decoration { get; set; } = null!;

    public Guid ShelfId { get; set; }

    [ForeignKey("ShelfId")]
    public Shelf Shelf { get; set; } = null!;

    /// <summary>Position in the shelf's shared coordinate system (alongside Books and Plants).</summary>
    public int Position { get; set; } = 0;
}
