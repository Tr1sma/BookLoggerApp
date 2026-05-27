using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Core.Helpers;

/// <summary>
/// Provides a static mapping between well-known genre IDs and their relevant rating categories.
/// </summary>
public static class GenreRatingMapping
{
    private static readonly Guid FictionId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid NonFictionId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid FantasyId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid SciFiId = Guid.Parse("00000000-0000-0000-0000-000000000004");
    private static readonly Guid MysteryId = Guid.Parse("00000000-0000-0000-0000-000000000005");
    private static readonly Guid RomanceId = Guid.Parse("00000000-0000-0000-0000-000000000006");
    private static readonly Guid BiographyId = Guid.Parse("00000000-0000-0000-0000-000000000007");
    private static readonly Guid HistoryId = Guid.Parse("00000000-0000-0000-0000-000000000008");
    private static readonly Guid DarkRomanceId = Guid.Parse("00000000-0000-0000-0000-000000000009");
    private static readonly Guid KrimiId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid ComedyId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly Guid ThrillerId = Guid.Parse("00000000-0000-0000-0000-000000000012");

    private static readonly Dictionary<Guid, HashSet<RatingCategory>> GenreCategoryMap = new()
    {
        [FictionId] = new HashSet<RatingCategory>
        {
            RatingCategory.Characters, RatingCategory.Plot, RatingCategory.WritingStyle,
            RatingCategory.Pacing, RatingCategory.EmotionaleTiefe, RatingCategory.Atmosphaere
        },
        [NonFictionId] = new HashSet<RatingCategory>
        {
            RatingCategory.WritingStyle, RatingCategory.Pacing, RatingCategory.Informationsgehalt
        },
        [FantasyId] = new HashSet<RatingCategory>
        {
            RatingCategory.Characters, RatingCategory.Plot, RatingCategory.WritingStyle,
            RatingCategory.Pacing, RatingCategory.WorldBuilding, RatingCategory.Atmosphaere
        },
        [SciFiId] = new HashSet<RatingCategory>
        {
            RatingCategory.Characters, RatingCategory.Plot, RatingCategory.WritingStyle,
            RatingCategory.Pacing, RatingCategory.WorldBuilding, RatingCategory.Atmosphaere
        },
        [MysteryId] = new HashSet<RatingCategory>
        {
            RatingCategory.Characters, RatingCategory.Plot, RatingCategory.WritingStyle,
            RatingCategory.Pacing, RatingCategory.Spannung, RatingCategory.Atmosphaere
        },
        [RomanceId] = new HashSet<RatingCategory>
        {
            RatingCategory.Characters, RatingCategory.Plot, RatingCategory.WritingStyle,
            RatingCategory.SpiceLevel, RatingCategory.Pacing, RatingCategory.EmotionaleTiefe
        },
        [BiographyId] = new HashSet<RatingCategory>
        {
            RatingCategory.WritingStyle, RatingCategory.Pacing, RatingCategory.Informationsgehalt
        },
        [HistoryId] = new HashSet<RatingCategory>
        {
            RatingCategory.WritingStyle, RatingCategory.Pacing, RatingCategory.Informationsgehalt
        },
        [DarkRomanceId] = new HashSet<RatingCategory>
        {
            RatingCategory.Characters, RatingCategory.Plot, RatingCategory.WritingStyle,
            RatingCategory.SpiceLevel, RatingCategory.Pacing, RatingCategory.EmotionaleTiefe,
            RatingCategory.Atmosphaere
        },
        [KrimiId] = new HashSet<RatingCategory>
        {
            RatingCategory.Characters, RatingCategory.Plot, RatingCategory.WritingStyle,
            RatingCategory.Pacing, RatingCategory.Spannung, RatingCategory.Atmosphaere
        },
        [ComedyId] = new HashSet<RatingCategory>
        {
            RatingCategory.Characters, RatingCategory.Plot, RatingCategory.WritingStyle,
            RatingCategory.Pacing, RatingCategory.Humor
        },
        [ThrillerId] = new HashSet<RatingCategory>
        {
            RatingCategory.Characters, RatingCategory.Plot, RatingCategory.WritingStyle,
            RatingCategory.Pacing, RatingCategory.Spannung, RatingCategory.Atmosphaere
        },
    };

    private static readonly HashSet<RatingCategory> AllCategories =
        new(Enum.GetValues<RatingCategory>());

    /// <summary>
    /// Returns the union of all relevant rating categories for the given genre IDs.
    /// If genreIds is null/empty or contains no recognized genres, returns ALL categories.
    /// </summary>
    public static IReadOnlySet<RatingCategory> GetRelevantCategories(IEnumerable<Guid>? genreIds)
    {
        if (genreIds is null)
            return AllCategories;

        var result = new HashSet<RatingCategory>();

        foreach (Guid genreId in genreIds)
        {
            if (GenreCategoryMap.TryGetValue(genreId, out HashSet<RatingCategory>? categories))
            {
                result.UnionWith(categories);
            }
        }

        return result.Count > 0 ? result : AllCategories;
    }

    /// <summary>
    /// Returns all categories NOT in the relevant set for the given genres.
    /// </summary>
    public static IReadOnlyList<RatingCategory> GetAdditionalCategories(IEnumerable<Guid>? genreIds)
    {
        IReadOnlySet<RatingCategory> relevant = GetRelevantCategories(genreIds);

        if (relevant.Count == AllCategories.Count)
            return Array.Empty<RatingCategory>();

        return AllCategories.Where(c => !relevant.Contains(c)).ToList();
    }

    /// <summary>
    /// Reads the rating value stored on the book for a given category.
    /// </summary>
    public static int? GetRatingValue(Book book, RatingCategory category) => category switch
    {
        RatingCategory.Characters => book.CharactersRating,
        RatingCategory.Plot => book.PlotRating,
        RatingCategory.WritingStyle => book.WritingStyleRating,
        RatingCategory.SpiceLevel => book.SpiceLevelRating,
        RatingCategory.Pacing => book.PacingRating,
        RatingCategory.WorldBuilding => book.WorldBuildingRating,
        RatingCategory.Spannung => book.SpannungRating,
        RatingCategory.Humor => book.HumorRating,
        RatingCategory.Informationsgehalt => book.InformationsgehaltRating,
        RatingCategory.EmotionaleTiefe => book.EmotionaleTiefeRating,
        RatingCategory.Atmosphaere => book.AtmosphaereRating,
        _ => null
    };

    /// <summary>
    /// True when every rating category relevant to the book's genres has a value.
    /// Mirrors the categories shown as "primary" in the rating UI.
    /// </summary>
    public static bool IsFullyRated(Book book, IEnumerable<Guid>? genreIds)
    {
        IReadOnlySet<RatingCategory> relevant = GetRelevantCategories(genreIds);
        return relevant.All(c => GetRatingValue(book, c).HasValue);
    }
}
