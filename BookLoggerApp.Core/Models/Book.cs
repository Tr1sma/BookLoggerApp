using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>A book in the user's library.</summary>
public class Book
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Author { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ISBN { get; set; }

    [MaxLength(200)]
    public string? Publisher { get; set; }

    public int? PublicationYear { get; set; }

    [MaxLength(50)]
    public string? Language { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public int? PageCount { get; set; }

    public int CurrentPage { get; set; } = 0;

    [MaxLength(500)]
    public string? CoverImagePath { get; set; }

    [MaxLength(20)]
    public string? SpineColor { get; set; } // Spine color identifier (e.g., "red", "blue").

    public bool UsesCoverAsSpine { get; set; } = false; // Use cover image as spine background instead of color.

    [MaxLength(20)]
    public string? BookshelfPosition { get; set; } // Position for drag-and-drop bookshelf sorting.

    public ReadingStatus Status { get; set; } = ReadingStatus.Planned;

    // Multi-category ratings, 1-5 stars, nullable.
    public int? CharactersRating { get; set; }
    public int? PlotRating { get; set; }
    public int? WritingStyleRating { get; set; }
    public int? SpiceLevelRating { get; set; }
    public int? PacingRating { get; set; }
    public int? WorldBuildingRating { get; set; }
    public int? SpannungRating { get; set; }
    public int? HumorRating { get; set; }
    public int? InformationsgehaltRating { get; set; }
    public int? EmotionaleTiefeRating { get; set; }
    public int? AtmosphaereRating { get; set; }


    [MaxLength(5000)]
    public string? Notes { get; set; }

    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    public DateTime? DateStarted { get; set; }
    public DateTime? DateCompleted { get; set; }

    public ICollection<BookGenre> BookGenres { get; set; } = new List<BookGenre>();
    public ICollection<BookTrope> BookTropes { get; set; } = new List<BookTrope>();
    public ICollection<ReadingSession> ReadingSessions { get; set; } = new List<ReadingSession>();
    public ICollection<Quote> Quotes { get; set; } = new List<Quote>();
    public ICollection<Annotation> Annotations { get; set; } = new List<Annotation>();
    public ICollection<BookShelf> BookShelves { get; set; } = new List<BookShelf>();

    // Wishlist metadata (1:1, only for Wishlist status books)
    public WishlistInfo? WishlistInfo { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public int ProgressPercentage => PageCount.HasValue && PageCount.Value > 0
        ? Math.Clamp(CurrentPage * 100 / PageCount.Value, 0, 100)
        : 0;

    /// <summary>
    /// Average of all set category ratings, or null if none set. SpiceLevelRating is excluded —
    /// it measures romantic intensity, not quality, so it must not skew the average.
    /// </summary>
    public double? AverageRating
    {
        get
        {
            var ratings = new List<int?>
            {
                CharactersRating,
                PlotRating,
                WritingStyleRating,
                PacingRating,
                WorldBuildingRating,
                SpannungRating,
                HumorRating,
                InformationsgehaltRating,
                EmotionaleTiefeRating,
                AtmosphaereRating
            };

            var validRatings = ratings.Where(r => r.HasValue).Select(r => r!.Value).ToList();

            if (!validRatings.Any())
                return null;

            return validRatings.Average();
        }
    }
}

/// <summary>Reading state for a book.</summary>
public enum ReadingStatus
{
    Planned = 0,
    Reading = 1,
    Completed = 2,
    Abandoned = 3,
    Wishlist = 4
}
