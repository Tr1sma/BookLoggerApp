using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Helpers;

/// <summary>
/// Helper class for calculating and retrieving book ratings.
/// </summary>
public static class RatingHelper
{
    /// <summary>
    /// Calculates the average of all set category ratings for a book.
    /// </summary>
    public static double? CalculateAverage(Book book)
    {
        var ratings = new List<int?>
        {
            book.CharactersRating,
            book.PlotRating,
            book.WritingStyleRating,
            book.SpiceLevelRating,
            book.PacingRating,
            book.WorldBuildingRating
        };

        var validRatings = ratings.Where(r => r.HasValue).Select(r => r!.Value).ToList();

        if (!validRatings.Any())
            return null;

        return validRatings.Average();
    }

    /// <summary>
    /// Retrieves the rating for a specific category from a book.
    /// </summary>
    public static int? GetRating(Book book, RatingCategory category)
    {
        return category switch
        {
            RatingCategory.Characters => book.CharactersRating,
            RatingCategory.Plot => book.PlotRating,
            RatingCategory.WritingStyle => book.WritingStyleRating,
            RatingCategory.SpiceLevel => book.SpiceLevelRating,
            RatingCategory.Pacing => book.PacingRating,
            RatingCategory.WorldBuilding => book.WorldBuildingRating,
            _ => null
        };
    }
}
