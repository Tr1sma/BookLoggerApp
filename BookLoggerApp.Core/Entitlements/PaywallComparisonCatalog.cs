using BookLoggerApp.Core.Resources;
using Microsoft.Extensions.Localization;

namespace BookLoggerApp.Core.Entitlements;

/// <summary>
/// Ordered, tier-grouped feature-comparison rows for the paywall modal.
/// Decoupled from <see cref="FeatureKey"/> on purpose: this catalog is for marketing
/// presentation. Runtime gating still goes through <see cref="FeaturePolicy"/>.
/// Rows are grouped by availability so the user sees "what everyone gets", then
/// "what Plus adds", then "what Premium adds on top". Keep rows stable within each
/// section — the paywall renders them in this order.
/// </summary>
public static class PaywallComparisonCatalog
{
    public enum Category
    {
        /// Feature is identical across Free, Plus, and Premium.
        AllPlans,

        /// Feature is available in both Plus and Premium (Free is either limited or missing).
        PlusAndPremium,

        /// Feature is exclusive to Premium.
        PremiumOnly
    }

    /// <summary>
    /// One row in the comparison table. <see cref="FreeValue"/>, <see cref="PlusValue"/>,
    /// and <see cref="PremiumValue"/> are either a glyph ("✓" / "—" / "∞") or a small
    /// number ("3", "4"). <see cref="MappedFeature"/> is set when the row corresponds to
    /// a gated <see cref="FeatureKey"/>, enabling the paywall to highlight it when opened
    /// via that trigger. <see cref="LabelKey"/> is the resource key for the neutral
    /// English label so the paywall can localize it; if null, <see cref="Label"/> is
    /// displayed verbatim.
    /// </summary>
    public record Row(
        Category Category,
        string Label,
        string FreeValue,
        string PlusValue,
        string PremiumValue,
        FeatureKey? MappedFeature = null,
        string? LabelKey = null);

    public static IReadOnlyList<Row> Rows { get; } = new[]
    {
        // All plans — identical across every tier
        new Row(Category.AllPlans, "Books", "∞", "∞", "∞", LabelKey: "Paywall_Feature_Books"),
        new Row(Category.AllPlans, "Basic stats", "✓", "✓", "✓", LabelKey: "Paywall_Feature_BasicStats"),
        new Row(Category.AllPlans, "ISBN / barcode scanner", "✓", "✓", "✓", LabelKey: "Paywall_Feature_IsbnScanner"),
        new Row(Category.AllPlans, "Backup & export (CSV, ZIP)", "✓", "✓", "✓", LabelKey: "Paywall_Feature_BackupExport"),
        new Row(Category.AllPlans, "Home widgets", "✓", "✓", "✓", LabelKey: "Paywall_Feature_HomeWidgets"),

        // Plus & Premium — content limits and unlocks that Free does not get (or gets a sample of)
        new Row(Category.PlusAndPremium, "Notes & quotes", "3", "∞", "∞", FeatureKey.UnlimitedNotesAndQuotes, "Paywall_Feature_NotesQuotes"),
        new Row(Category.PlusAndPremium, "Reading goals", "3", "∞", "∞", FeatureKey.UnlimitedReadingGoals, "Paywall_Feature_ReadingGoals"),
        new Row(Category.PlusAndPremium, "Shelves", "3", "∞", "∞", FeatureKey.UnlimitedShelves, "Paywall_Feature_Shelves"),
        new Row(Category.PlusAndPremium, "Plants", "4", "∞", "∞", FeatureKey.StandardPlantsAndDecorations, "Paywall_Feature_Plants"),
        new Row(Category.PlusAndPremium, "Decorations", "3", "∞", "∞", FeatureKey.StandardPlantsAndDecorations, "Paywall_Feature_Decorations"),
        new Row(Category.PlusAndPremium, "Themes", "1", "∞", "∞", FeatureKey.PremiumThemes, "Paywall_Feature_Themes"),
        new Row(Category.PlusAndPremium, "Wishlist", "—", "✓", "✓", FeatureKey.Wishlist, "Paywall_Feature_Wishlist"),
        new Row(Category.PlusAndPremium, "Tropes", "—", "✓", "✓", FeatureKey.Tropes, "Paywall_Feature_Tropes"),
        new Row(Category.PlusAndPremium, "Shelf colors", "—", "✓", "✓", FeatureKey.CustomShelfColors, "Paywall_Feature_ShelfColors"),

        // Premium only — exclusive to Premium
        new Row(Category.PremiumOnly, "Trends (heatmap, radar)", "—", "—", "✓", FeatureKey.StatsTrendsTab, "Paywall_Feature_Trends"),
        new Row(Category.PremiumOnly, "Insights (year, top authors)", "—", "—", "✓", FeatureKey.StatsInsightsTab, "Paywall_Feature_Insights"),
        new Row(Category.PremiumOnly, "Share cards (Wrapped, books)", "—", "—", "✓", FeatureKey.ShareCards, "Paywall_Feature_ShareCards"),
        new Row(Category.PremiumOnly, "Prestige plants", "—", "—", "✓", FeatureKey.PrestigePlants, "Paywall_Feature_PrestigePlants"),
        new Row(Category.PremiumOnly, "Heart of Stories", "—", "—", "✓", FeatureKey.UltimateDecorations, "Paywall_Feature_HeartOfStories"),
        new Row(Category.PremiumOnly, "Genre / trope filter", "—", "—", "✓", FeatureKey.ReadingGoalsWithGenreTropeFilter, "Paywall_Feature_GenreTropeFilter"),
        new Row(Category.PremiumOnly, "Family sharing", "—", "—", "✓", FeatureKey.FamilySharing, "Paywall_Feature_FamilySharing"),
        new Row(Category.PremiumOnly, "Feature suggestions", "—", "—", "✓", FeatureKey.FeatureSuggestionForm, "Paywall_Feature_FeatureSuggestions"),
    };

    /// <summary>
    /// Localized label for a row — uses <see cref="Row.LabelKey"/> when present and
    /// the key resolves, otherwise falls back to <see cref="Row.Label"/>.
    /// </summary>
    public static string GetLocalizedLabel(Row row, IStringLocalizer<AppResources> l)
    {
        if (row.LabelKey is null)
        {
            return row.Label;
        }
        LocalizedString value = l[row.LabelKey];
        return value.ResourceNotFound ? row.Label : value.Value;
    }

    public static string GetCategoryLabel(Category category) => category switch
    {
        Category.AllPlans => "All plans",
        Category.PlusAndPremium => "Plus & Premium",
        Category.PremiumOnly => "Premium only",
        _ => category.ToString()
    };

    public static string GetLocalizedCategoryLabel(Category category, IStringLocalizer<AppResources> l)
    {
        string key = category switch
        {
            Category.AllPlans => "Paywall_Category_AllPlans",
            Category.PlusAndPremium => "Paywall_Category_PlusAndPremium",
            Category.PremiumOnly => "Paywall_Category_PremiumOnly",
            _ => string.Empty,
        };
        if (string.IsNullOrEmpty(key))
        {
            return GetCategoryLabel(category);
        }
        LocalizedString value = l[key];
        return value.ResourceNotFound ? GetCategoryLabel(category) : value.Value;
    }
}
