using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a bookshelf that can contain multiple books.
/// </summary>
public class Shelf
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// If set, the shelf automatically populates with books matching this criteria.
    /// If None, it's a manual shelf.
    /// </summary>
    public ShelfAutoSortRule AutoSortRule { get; set; } = ShelfAutoSortRule.None;

    /// <summary>
    /// Display order of the shelf on the dashboard/bookshelf page.
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// Icon or emoji for the shelf header using existing font/emoji support.
    /// </summary>
    [MaxLength(20)]
    public string Icon { get; set; } = "📚";

    // Navigation
    public ICollection<BookShelf> BookShelves { get; set; } = new List<BookShelf>();
    public ICollection<PlantShelf> PlantShelves { get; set; } = new List<PlantShelf>();
}

public enum ShelfAutoSortRule
{
    None = 0,
    StatusPlanned = 1,
    StatusReading = 2,
    StatusCompleted = 3,
    StatusAbandoned = 4,
    StatusWishlist = 5
}
