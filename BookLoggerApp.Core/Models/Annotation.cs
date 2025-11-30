using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a user note/annotation for a book.
/// </summary>
public class Annotation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    // Annotation Data
    [Required]
    [MaxLength(5000)]
    public string Note { get; set; } = string.Empty;

    [Range(0, 10000)]
    public int? PageNumber { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; } // Optional title

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Optional: Color tag
    [MaxLength(7)]
    public string? ColorHex { get; set; }

    // Concurrency Control
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
