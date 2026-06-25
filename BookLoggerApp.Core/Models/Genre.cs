using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>A book genre/category.</summary>
public class Genre
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; } // Icon name or emoji

    [MaxLength(7)]
    public string? ColorHex { get; set; } // For UI theming

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public ICollection<BookGenre> BookGenres { get; set; } = new List<BookGenre>();
    public ICollection<Trope> Tropes { get; set; } = new List<Trope>();
}
