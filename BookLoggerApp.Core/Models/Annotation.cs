using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>A user note/annotation for a book.</summary>
public class Annotation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    [Required]
    [MaxLength(5000)]
    public string Note { get; set; } = string.Empty;

    [Range(0, 10000)]
    public int? PageNumber { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(7)]
    public string? ColorHex { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
