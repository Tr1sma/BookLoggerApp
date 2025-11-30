using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Many-to-many relationship between Books and Genres.
/// </summary>
public class BookGenre
{
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    public Guid GenreId { get; set; }
    public Genre Genre { get; set; } = null!;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    // Concurrency Control
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
