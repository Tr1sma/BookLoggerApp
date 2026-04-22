namespace BookLoggerApp.Core.Services.Analytics;

// Firebase reserves several event names (e.g., "session_start", "app_open", "first_open",
// "screen_view", "app_update"). We deliberately use past-tense / distinct variants
// (e.g., "session_started" instead of "session_start") to avoid reserved-name collisions.
public static class AnalyticsEventNames
{
    // Book lifecycle
    public const string BookAdded = "book_added";
    public const string BookStatusChanged = "book_status_changed";
    public const string BookCompleted = "book_completed";
    public const string BookDeleted = "book_deleted";
    public const string BookRatingSubmitted = "book_rating_submitted";
    public const string BookMovedToShelf = "book_moved_to_shelf";

    // Reading sessions
    public const string SessionStarted = "session_started";
    public const string SessionPaused = "session_paused";
    public const string SessionResumed = "session_resumed";
    public const string SessionEnded = "session_ended";
    public const string SessionCancelled = "session_cancelled";
    public const string SessionCelebrationShown = "session_celebration_shown";

    // Progression & gamification
    public const string LevelUp = "level_up_event";
    public const string XpEarned = "xp_earned";
    public const string CoinsEarned = "coins_earned";
    public const string CoinsSpent = "coins_spent";

    // Plants
    public const string PlantPurchased = "plant_purchased";
    public const string PlantWatered = "plant_watered";
    public const string PlantDied = "plant_died";
    public const string PlantGrowthStageReached = "plant_growth_stage_reached";

    // Decorations
    public const string DecorationPurchased = "decoration_purchased";
    public const string DecorationPlacedOnShelf = "decoration_placed_on_shelf";

    // Goals
    public const string GoalCreated = "goal_created";
    public const string GoalCompleted = "goal_completed";
    public const string GoalAbandoned = "goal_abandoned";

    // Content
    public const string QuoteAdded = "quote_added";
    public const string AnnotationAdded = "annotation_added";
    public const string WishlistAdded = "wishlist_added";
    public const string WishlistPromotedToBook = "wishlist_promoted_to_book";
    public const string ShelfCreated = "shelf_created";

    // Onboarding & missions
    public const string MissionCompleted = "mission_completed";
    public const string OnboardingStepViewed = "onboarding_step_viewed";
    public const string OnboardingCompleted = "onboarding_completed_event";

    // App lifecycle meta
    public const string ChangelogViewed = "changelog_viewed";
    public const string ReviewPromptShown = "review_prompt_shown";
    public const string ReviewPromptAction = "review_prompt_action";
    public const string UpdatePromptShown = "update_prompt_shown";
    public const string UpdatePromptAction = "update_prompt_action";
    public const string WidgetOpenedApp = "widget_opened_app";
    public const string AppSettingsChanged = "app_settings_changed";

    // Import/export/backup
    public const string BackupCreated = "backup_created";
    public const string BackupRestored = "backup_restored";
    public const string ImportCompleted = "import_completed";
    public const string ExportCompleted = "export_completed";

    // ISBN scanning
    public const string IsbnScanStarted = "isbn_scan_started";
    public const string IsbnScanSuccess = "isbn_scan_success";
    public const string IsbnScanFailure = "isbn_scan_failure";

    // UI interactions
    public const string FilterApplied = "filter_applied";
    public const string SortChanged = "sort_changed";
    public const string TabSwitched = "tab_switched";
    public const string SearchPerformed = "search_performed";
    public const string QuickTimerOpened = "quick_timer_opened";
    public const string BackButtonPressed = "back_button_pressed";
    public const string SheetOpened = "sheet_opened";
    public const string BottomNavTabSelected = "bottom_nav_tab_selected";
    public const string CelebrationShown = "celebration_shown";
    public const string CelebrationDismissed = "celebration_dismissed";
    public const string ShareCardGenerated = "share_card_generated";
    public const string ShareCardShared = "share_card_shared";
    public const string BuyMeACoffeeClicked = "buy_me_a_coffee_clicked";
    public const string PrivacyBannerShown = "privacy_banner_shown";
    public const string PrivacyBannerAction = "privacy_banner_action";
}
