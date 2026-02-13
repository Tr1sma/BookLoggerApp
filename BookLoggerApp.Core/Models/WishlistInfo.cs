using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Stores wishlist-specific metadata for a book.
/// One-to-one relationship with Book (only when Book.Status == Wishlist).
/// </summary>
public class WishlistInfo
{
    [Key]
    public Guid BookId { get; set; }

    [ForeignKey("BookId")]
    public Book Book { get; set; } = null!;

    public WishlistPriority Priority { get; set; } = WishlistPriority.Medium;

    [MaxLength(200)]
    public string? RecommendedBy { get; set; }

    [MaxLength(1000)]
    public string? WishlistNotes { get; set; }

    public DateTime DateAddedToWishlist { get; set; } = DateTime.UtcNow;
}
