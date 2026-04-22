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
    /// via that trigger.
    /// </summary>
    public record Row(
        Category Category,
        string Label,
        string FreeValue,
        string PlusValue,
        string PremiumValue,
        FeatureKey? MappedFeature = null);

    public static IReadOnlyList<Row> Rows { get; } = new[]
    {
        // All plans — identical across every tier
        new Row(Category.AllPlans, "Books", "∞", "∞", "∞"),
        new Row(Category.AllPlans, "Basic stats", "✓", "✓", "✓"),
        new Row(Category.AllPlans, "ISBN / barcode scanner", "✓", "✓", "✓"),
        new Row(Category.AllPlans, "Backup & export (CSV, ZIP)", "✓", "✓", "✓"),
        new Row(Category.AllPlans, "Home widgets", "✓", "✓", "✓"),

        // Plus & Premium — content limits and unlocks that Free does not get (or gets a sample of)
        new Row(Category.PlusAndPremium, "Notes & quotes", "3", "∞", "∞", FeatureKey.UnlimitedNotesAndQuotes),
        new Row(Category.PlusAndPremium, "Reading goals", "3", "∞", "∞", FeatureKey.UnlimitedReadingGoals),
        new Row(Category.PlusAndPremium, "Shelves", "3", "∞", "∞", FeatureKey.UnlimitedShelves),
        new Row(Category.PlusAndPremium, "Plants", "4", "∞", "∞", FeatureKey.StandardPlantsAndDecorations),
        new Row(Category.PlusAndPremium, "Decorations", "3", "∞", "∞", FeatureKey.StandardPlantsAndDecorations),
        new Row(Category.PlusAndPremium, "Themes", "1", "∞", "∞", FeatureKey.PremiumThemes),
        new Row(Category.PlusAndPremium, "Wishlist", "—", "✓", "✓", FeatureKey.Wishlist),
        new Row(Category.PlusAndPremium, "Tropes", "—", "✓", "✓", FeatureKey.Tropes),
        new Row(Category.PlusAndPremium, "Shelf colors", "—", "✓", "✓", FeatureKey.CustomShelfColors),

        // Premium only — exclusive to Premium
        new Row(Category.PremiumOnly, "Trends (heatmap, radar)", "—", "—", "✓", FeatureKey.StatsTrendsTab),
        new Row(Category.PremiumOnly, "Insights (year, top authors)", "—", "—", "✓", FeatureKey.StatsInsightsTab),
        new Row(Category.PremiumOnly, "Share cards (Wrapped, books)", "—", "—", "✓", FeatureKey.ShareCards),
        new Row(Category.PremiumOnly, "Prestige plants", "—", "—", "✓", FeatureKey.PrestigePlants),
        new Row(Category.PremiumOnly, "Heart of Stories", "—", "—", "✓", FeatureKey.UltimateDecorations),
        new Row(Category.PremiumOnly, "Genre / trope filter", "—", "—", "✓", FeatureKey.ReadingGoalsWithGenreTropeFilter),
        new Row(Category.PremiumOnly, "Family sharing", "—", "—", "✓", FeatureKey.FamilySharing),
        new Row(Category.PremiumOnly, "Feature suggestions", "—", "—", "✓", FeatureKey.FeatureSuggestionForm),
    };

    public static string GetCategoryLabel(Category category) => category switch
    {
        Category.AllPlans => "All plans",
        Category.PlusAndPremium => "Plus & Premium",
        Category.PremiumOnly => "Premium only",
        _ => category.ToString()
    };
}
