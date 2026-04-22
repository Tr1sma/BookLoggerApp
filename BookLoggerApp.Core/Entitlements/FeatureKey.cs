namespace BookLoggerApp.Core.Entitlements;

/// <summary>
/// Stable identifier for every entitlement-gated feature or capability.
/// Do NOT reorder or reuse numeric values — these keys may be persisted in analytics.
/// </summary>
public enum FeatureKey
{
    // Content limits
    UnlimitedNotesAndQuotes = 1,
    UnlimitedReadingGoals = 2,
    ReadingGoalsWithGenreTropeFilter = 3,
    UnlimitedShelves = 4,
    CustomShelfColors = 5,

    // Shop gating
    StandardPlantsAndDecorations = 6,
    PrestigePlants = 7,
    UltimateDecorations = 8,

    // Stats & insights
    StatsTrendsTab = 9,
    StatsInsightsTab = 10,

    // Sharing
    ShareCards = 11,

    // Optional organization features
    Wishlist = 12,
    Tropes = 13,

    // Personalization
    PremiumThemes = 14,

    // Premium-exclusive extras
    FeatureSuggestionForm = 15,
    FamilySharing = 16
}
