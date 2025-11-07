using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a favorite quote from a book.
/// </summary>
public class Quote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    // Quote Data
    [Required]
    [MaxLength(2000)]
    public string Text { get; set; } = string.Empty;

    [Range(0, 10000)]
    public int? PageNumber { get; set; }

    [MaxLength(500)]
    public string? Context { get; set; } // Optional context note

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsFavorite { get; set; } = false;

    // Concurrency Control
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
