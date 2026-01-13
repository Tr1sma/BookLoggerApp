using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a sub-genre or literary trope associated with a specific Genre.
/// </summary>
public class Trope
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // Foreign Key for Genre
    public Guid GenreId { get; set; }
    
    [ForeignKey(nameof(GenreId))]
    public Genre Genre { get; set; } = null!;

    // Navigation Properties
    public ICollection<BookTrope> BookTropes { get; set; } = new List<BookTrope>();
}
