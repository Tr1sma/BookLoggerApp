using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoggerApp.Core.Models;

/// <summary>Many-to-many join between Book and Trope.</summary>
public class BookTrope
{
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    public Guid TropeId { get; set; }
    public Trope Trope { get; set; } = null!;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
