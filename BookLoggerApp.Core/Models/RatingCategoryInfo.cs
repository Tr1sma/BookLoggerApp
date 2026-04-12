namespace BookLoggerApp.Core.Models;

/// <summary>
/// Provides metadata and display information for rating categories.
/// </summary>
public class RatingCategoryInfo
{
    public RatingCategory Category { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Returns all available rating categories with their metadata.
    /// </summary>
    public static List<RatingCategoryInfo> GetAllCategories()
    {
        return new List<RatingCategoryInfo>
        {
            new()
            {
                Category = RatingCategory.Characters,
                Emoji = "👥",
                DisplayName = "Characters",
                Description = "Character quality and development"
            },
            new()
            {
                Category = RatingCategory.Plot,
                Emoji = "📖",
                DisplayName = "Plot",
                Description = "Story and storyline"
            },
            new()
            {
                Category = RatingCategory.WritingStyle,
                Emoji = "✍️",
                DisplayName = "Writing Style",
                Description = "Author's writing style"
            },
            new()
            {
                Category = RatingCategory.SpiceLevel,
                Emoji = "🌶️",
                DisplayName = "Spice Level",
                Description = "Romance/Spice level"
            },
            new()
            {
                Category = RatingCategory.Pacing,
                Emoji = "⚡",
                DisplayName = "Pacing",
                Description = "Story tempo"
            },
            new()
            {
                Category = RatingCategory.WorldBuilding,
                Emoji = "🌍",
                DisplayName = "World Building",
                Description = "World building quality"
            },
            new()
            {
                Category = RatingCategory.Spannung,
                Emoji = "😰",
                DisplayName = "Spannung",
                Description = "Spannung und Nervenkitzel"
            },
            new()
            {
                Category = RatingCategory.Humor,
                Emoji = "😂",
                DisplayName = "Humor",
                Description = "Humor und Unterhaltung"
            },
            new()
            {
                Category = RatingCategory.Informationsgehalt,
                Emoji = "💡",
                DisplayName = "Informationsgehalt",
                Description = "Informationsgehalt und Tiefe"
            },
            new()
            {
                Category = RatingCategory.EmotionaleTiefe,
                Emoji = "💖",
                DisplayName = "Emotionale Tiefe",
                Description = "Emotionale Tiefe und Berührung"
            },
            new()
            {
                Category = RatingCategory.Atmosphaere,
                Emoji = "🌙",
                DisplayName = "Atmosphäre",
                Description = "Atmosphäre und Stimmung"
            },
        };
    }

    /// <summary>
    /// Gets the rating category info for a specific category.
    /// </summary>
    public static RatingCategoryInfo? GetCategoryInfo(RatingCategory category)
    {
        return GetAllCategories().FirstOrDefault(c => c.Category == category);
    }
}
