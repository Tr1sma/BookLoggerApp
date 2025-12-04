using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a book in the user's library.
/// </summary>
public class Book
{
    // Primary Key
    public Guid Id { get; set; } = Guid.NewGuid();

    // Basic Info
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Author { get; set; } = string.Empty;

    [MaxLength(13)]
    public string? ISBN { get; set; }

    [MaxLength(200)]
    public string? Publisher { get; set; }

    public int? PublicationYear { get; set; }

    [MaxLength(50)]
    public string? Language { get; set; }

    // Content
    [MaxLength(2000)]
    public string? Description { get; set; }

    public int? PageCount { get; set; }

    public int CurrentPage { get; set; } = 0;

    // Media
    [MaxLength(500)]
    public string? CoverImagePath { get; set; }

    [MaxLength(20)]
    public string? SpineColor { get; set; } // Color identifier for book spine (e.g., "red", "blue", "green")

    public bool UsesCoverAsSpine { get; set; } = false; // If true, use cover image as spine background instead of color

    [MaxLength(20)]
    public string? BookshelfPosition { get; set; } // Position on bookshelf for drag & drop sorting

    // Status & Rating
    public ReadingStatus Status { get; set; } = ReadingStatus.Planned;

    [Obsolete("Use OverallRating instead")]
    public int? Rating
    {
        get => OverallRating;
        set => OverallRating = value;
    }

    // Multi-Category Ratings (1-5 stars, nullable)
    public int? CharactersRating { get; set; }
    public int? PlotRating { get; set; }
    public int? WritingStyleRating { get; set; }
    public int? SpiceLevelRating { get; set; }
    public int? PacingRating { get; set; }
    public int? WorldBuildingRating { get; set; }
    public int? OverallRating { get; set; }

    // Timestamps
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    public DateTime? DateStarted { get; set; }
    public DateTime? DateCompleted { get; set; }

    // Navigation Properties
    public ICollection<BookGenre> BookGenres { get; set; } = new List<BookGenre>();
    public ICollection<ReadingSession> ReadingSessions { get; set; } = new List<ReadingSession>();
    public ICollection<Quote> Quotes { get; set; } = new List<Quote>();
    public ICollection<Annotation> Annotations { get; set; } = new List<Annotation>();

    // Concurrency Control
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    // Computed Properties
    public int ProgressPercentage => PageCount.HasValue && PageCount.Value > 0
        ? (CurrentPage * 100 / PageCount.Value)
        : 0;

    /// <summary>
    /// Calculates the average of all set category ratings.
    /// Returns OverallRating if no category ratings are set.
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
                SpiceLevelRating,
                PacingRating,
                WorldBuildingRating
            };

            var validRatings = ratings.Where(r => r.HasValue).Select(r => r!.Value).ToList();

            if (!validRatings.Any())
                return OverallRating;

            return validRatings.Average();
        }
    }
}

/// <summary>
/// Reading state for a book.
/// </summary>
public enum ReadingStatus
{
    Planned = 0,
    Reading = 1,
    Completed = 2,
    Abandoned = 3
}
