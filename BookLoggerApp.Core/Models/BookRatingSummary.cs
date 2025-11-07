namespace BookLoggerApp.Core.Models;

/// <summary>
/// DTO containing a book with its rating summary information.
/// </summary>
public class BookRatingSummary
{
    public Book Book { get; set; } = null!;

    /// <summary>
    /// Average of all category ratings (excluding Overall).
    /// </summary>
    public double AverageRating { get; set; }

    /// <summary>
    /// Dictionary of all ratings by category.
    /// </summary>
    public Dictionary<RatingCategory, int?> Ratings { get; set; } = new();

    /// <summary>
    /// Creates a BookRatingSummary from a Book instance.
    /// </summary>
    public static BookRatingSummary FromBook(Book book)
    {
        var summary = new BookRatingSummary
        {
            Book = book,
            AverageRating = book.AverageRating ?? book.OverallRating ?? 0,
            Ratings = new Dictionary<RatingCategory, int?>
            {
                { RatingCategory.Characters, book.CharactersRating },
                { RatingCategory.Plot, book.PlotRating },
                { RatingCategory.WritingStyle, book.WritingStyleRating },
                { RatingCategory.SpiceLevel, book.SpiceLevelRating },
                { RatingCategory.Pacing, book.PacingRating },
                { RatingCategory.WorldBuilding, book.WorldBuildingRating },
                { RatingCategory.Overall, book.OverallRating }
            }
        };

        return summary;
    }
}
