using BookLoggerApp.Core.Resources;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Core.Models;

/// <summary>Metadata and display info for rating categories.</summary>
public class RatingCategoryInfo
{
    public RatingCategory Category { get; set; }
    public string Emoji { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Returns all rating categories with their metadata.</summary>
    public static List<RatingCategoryInfo> GetAllCategories(IStringLocalizer<AppResources>? localizer = null)
    {
        return new List<RatingCategoryInfo>
        {
            Create(RatingCategory.Characters, "👥", "Characters", "Character quality and development", localizer),
            Create(RatingCategory.Plot, "📖", "Plot", "Story and storyline", localizer),
            Create(RatingCategory.WritingStyle, "✍️", "Writing Style", "Author's writing style", localizer),
            Create(RatingCategory.SpiceLevel, "🌶️", "Spice Level", "Romance/Spice level", localizer),
            Create(RatingCategory.Pacing, "⚡", "Pacing", "Story tempo", localizer),
            Create(RatingCategory.WorldBuilding, "🌍", "World Building", "World building quality", localizer),
            Create(RatingCategory.Spannung, "😰", "Tension", "Tension and suspense", localizer),
            Create(RatingCategory.Humor, "😂", "Humor", "Humor and entertainment", localizer),
            Create(RatingCategory.Informationsgehalt, "💡", "Information Content", "Information content and depth", localizer),
            Create(RatingCategory.EmotionaleTiefe, "💖", "Emotional Depth", "Emotional depth and resonance", localizer),
            Create(RatingCategory.Atmosphaere, "🌙", "Atmosphere", "Atmosphere and mood", localizer),
        };
    }

    /// <summary>Gets the info for a specific rating category.</summary>
    public static RatingCategoryInfo? GetCategoryInfo(
        RatingCategory category,
        IStringLocalizer<AppResources>? localizer = null)
    {
        return GetAllCategories(localizer).FirstOrDefault(c => c.Category == category);
    }

    private static RatingCategoryInfo Create(
        RatingCategory category,
        string emoji,
        string fallbackDisplayName,
        string fallbackDescription,
        IStringLocalizer<AppResources>? localizer)
    {
        string keyPrefix = $"RatingCategory_{category}";

        return new RatingCategoryInfo
        {
            Category = category,
            Emoji = emoji,
            DisplayName = GetLocalized(localizer, $"{keyPrefix}_DisplayName", fallbackDisplayName),
            Description = GetLocalized(localizer, $"{keyPrefix}_Description", fallbackDescription)
        };
    }

    private static string GetLocalized(
        IStringLocalizer<AppResources>? localizer,
        string key,
        string fallback)
    {
        if (localizer is null)
        {
            return fallback;
        }

        var localized = localizer[key];
        return localized.ResourceNotFound ? fallback : localized.Value;
    }
}
