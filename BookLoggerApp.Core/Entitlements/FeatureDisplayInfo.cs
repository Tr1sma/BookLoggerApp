using BookLoggerApp.Core.Resources;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Core.Entitlements;

/// <summary>
/// Human-readable presentation data for a feature. Used by the paywall comparison
/// table, contextual paywall headers, and the Settings "Plus &amp; Premium" page.
/// Label and Description are always English (app UI language is English).
/// </summary>
public record FeatureDisplayInfo(string Label, string Description, string IconKey);

/// <summary>
/// Static lookup from <see cref="FeatureKey"/> to its display metadata.
/// IconKey is a neutral identifier (e.g. "note", "shelf"); the paywall UI maps it
/// to an actual glyph or SVG — no emoji is embedded here.
/// </summary>
public static class FeatureDisplay
{
    public static IReadOnlyDictionary<FeatureKey, FeatureDisplayInfo> Info { get; } =
        new Dictionary<FeatureKey, FeatureDisplayInfo>
        {
            [FeatureKey.UnlimitedNotesAndQuotes] = new(
                "Unlimited notes & quotes",
                "Write as many notes and quotes per book as you want — no 3-per-book cap.",
                "note"),
            [FeatureKey.UnlimitedReadingGoals] = new(
                "Unlimited reading goals",
                "Track as many goals as you want — no 3-goal cap.",
                "goal"),
            [FeatureKey.ReadingGoalsWithGenreTropeFilter] = new(
                "Goals with genre & trope filters",
                "Only count books from specific genres or tropes toward your goal.",
                "filter"),
            [FeatureKey.UnlimitedShelves] = new(
                "Unlimited shelves",
                "Organize your library in as many shelves as you like — no 3-shelf cap.",
                "shelf"),
            [FeatureKey.CustomShelfColors] = new(
                "Custom shelf colors",
                "Paint each shelf with its own ledge and base color.",
                "palette"),
            [FeatureKey.StandardPlantsAndDecorations] = new(
                "All plants & decorations",
                "Unlock the full shop beyond the 4 starter plants and 3 starter decorations.",
                "shop"),
            [FeatureKey.PrestigePlants] = new(
                "Prestige plants",
                "Grow the Chronicle Tree and the Eternal Phoenix Bonsai with unique bonuses.",
                "prestige-plant"),
            [FeatureKey.UltimateDecorations] = new(
                "Ultimate decoration",
                "Place the Heart of Stories for boosted XP, coins and plant growth.",
                "ultimate-deco"),
            [FeatureKey.StatsTrendsTab] = new(
                "Stats trends",
                "Heatmaps, weekday patterns, reading-time distribution and genre radar.",
                "trend"),
            [FeatureKey.StatsInsightsTab] = new(
                "Stats insights",
                "Year-over-year comparisons, top authors, completion rates and more.",
                "insight"),
            [FeatureKey.ShareCards] = new(
                "Share cards",
                "Generate your Reading Wrapped and book-recommendation cards as beautiful images.",
                "share"),
            [FeatureKey.Wishlist] = new(
                "Wishlist",
                "Keep a prioritized list of books you want to read next.",
                "wishlist"),
            [FeatureKey.Tropes] = new(
                "Tropes",
                "Tag books with tropes and filter your library by them.",
                "trope"),
            [FeatureKey.PremiumThemes] = new(
                "Extra themes",
                "Pick from additional color themes beyond the default.",
                "theme"),
            [FeatureKey.FeatureSuggestionForm] = new(
                "Suggest a feature",
                "Send feature requests directly to the developer from inside the app.",
                "suggest"),
            [FeatureKey.FamilySharing] = new(
                "Google Play Family Sharing",
                "Share your Premium subscription with up to 5 family members.",
                "family")
        };

    public static FeatureDisplayInfo Get(FeatureKey feature)
    {
        if (Info.TryGetValue(feature, out FeatureDisplayInfo? info))
        {
            return info;
        }

        throw new InvalidOperationException($"No FeatureDisplayInfo defined for {feature}.");
    }

    /// <summary>
    /// Returns the label for <paramref name="feature"/>, translated via the supplied
    /// localizer. Keys follow the pattern <c>Feature_{FeatureKeyEnumName}_Label</c>.
    /// Falls back to the English string from <see cref="Info"/> when the resource
    /// key is missing or the localizer is null.
    /// </summary>
    public static string GetLocalizedLabel(FeatureKey feature, IStringLocalizer<AppResources>? l)
    {
        FeatureDisplayInfo info = Get(feature);
        if (l is null)
        {
            return info.Label;
        }
        LocalizedString value = l[$"Feature_{feature}_Label"];
        return value.ResourceNotFound ? info.Label : value.Value;
    }

    /// <summary>
    /// Returns the description for <paramref name="feature"/>, translated via the
    /// supplied localizer. Keys follow the pattern
    /// <c>Feature_{FeatureKeyEnumName}_Description</c>.
    /// </summary>
    public static string GetLocalizedDescription(FeatureKey feature, IStringLocalizer<AppResources>? l)
    {
        FeatureDisplayInfo info = Get(feature);
        if (l is null)
        {
            return info.Description;
        }
        LocalizedString value = l[$"Feature_{feature}_Description"];
        return value.ResourceNotFound ? info.Description : value.Value;
    }
}
