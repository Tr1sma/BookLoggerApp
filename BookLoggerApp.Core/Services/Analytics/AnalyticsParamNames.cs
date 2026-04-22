namespace BookLoggerApp.Core.Services.Analytics;

public static class AnalyticsParamNames
{
    // Generic
    public const string Source = "source";
    public const string Action = "action";
    public const string Page = "page";
    public const string Type = "type";
    public const string Reason = "reason";
    public const string Format = "format";

    // Booleans
    public const string HasCover = "has_cover";
    public const string HasRating = "has_rating";
    public const string HasAnnotations = "has_annotations";
    public const string HasQuotes = "has_quotes";
    public const string HasResults = "has_results";
    public const string Handled = "handled";
    public const string AutoDismiss = "auto_dismiss";
    public const string Skipped = "skipped";
    public const string QuickTimer = "quick_timer";
    public const string FromReadingPage = "from_reading_page";
    public const string TriggeredLevelUp = "triggered_levelup";

    // Status
    public const string FromStatus = "from_status";
    public const string ToStatus = "to_status";

    // Bucketed numeric / duration
    public const string ProgressBucket = "progress_bucket";
    public const string SessionMinutesBucket = "session_minutes_bucket";
    public const string MinutesBucket = "minutes_bucket";
    public const string PagesBucket = "pages_bucket";
    public const string PagesReadBucket = "pages_read_bucket";
    public const string RatingBucket = "rating_bucket";
    public const string OverallBucket = "overall_bucket";
    public const string CategoryCount = "category_count";
    public const string ElapsedMinutesBucket = "elapsed_minutes_bucket";
    public const string XpEarnedBucket = "xp_earned_bucket";
    public const string NewLevelBucket = "new_level_bucket";
    public const string LevelAtPurchaseBucket = "level_at_purchase_bucket";
    public const string LevelAtDeathBucket = "level_at_death_bucket";
    public const string TotalPlantsBucket = "total_plants_bucket";
    public const string TotalShelvesBucket = "total_shelves_bucket";
    public const string DeltaBucket = "delta_bucket";
    public const string AmountBucket = "amount_bucket";
    public const string PriceBucket = "price_bucket";
    public const string TargetBucket = "target_bucket";
    public const string DaysToCompleteBucket = "days_to_complete_bucket";
    public const string DurationSecondsBucket = "duration_seconds_bucket";
    public const string CountBucket = "count_bucket";
    public const string SizeBucket = "size_bucket";
    public const string DaysSinceLastWaterBucket = "days_since_last_water_bucket";
    public const string PlantUnlocksCount = "plant_unlocks_count";

    // Keys / categorical
    public const string SpeciesKey = "species_key";
    public const string DecorationKey = "decoration_key";
    public const string MissionId = "mission_id";
    public const string Stage = "stage";
    public const string StepIndex = "step_index";
    public const string TotalSteps = "total_steps";
    public const string Priority = "priority";
    public const string Version = "version";
    public const string SettingKey = "setting_key";
    public const string FilterType = "filter_type";
    public const string SortKey = "sort_key";
    public const string TabKey = "tab_key";
    public const string SheetKey = "sheet_key";
    public const string Target = "target";
    public const string WidgetType = "widget_type";
    public const string GoalType = "goal_type";

    // Forbidden-key set used by AnalyticsParamBuilder (PII guard)
    public static readonly HashSet<string> Forbidden = new(StringComparer.OrdinalIgnoreCase)
    {
        "title", "book_title", "author", "isbn", "quote", "quote_text", "annotation",
        "annotation_text", "note", "notes", "name", "book_name", "shelf_name",
        "plant_nickname", "nickname", "genre_name", "email", "user_name", "username",
        "first_name", "last_name", "full_name", "address", "phone", "location",
        "lat", "lon", "latitude", "longitude", "ip", "advertising_id", "idfa",
        "user_text", "search_query", "query"
    };
}
