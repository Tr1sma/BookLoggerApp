using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>A favorite quote from a book.</summary>
public class Quote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    [Required]
    [MaxLength(2000)]
    public string Text { get; set; } = string.Empty;

    [Range(0, 10000)]
    public int? PageNumber { get; set; }

    [MaxLength(500)]
    public string? Context { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsFavorite { get; set; } = false;

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
