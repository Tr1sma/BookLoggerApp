namespace BookLoggerApp.Core.Models;

public static class OnboardingMissionCatalog
{
    public const int CurrentFlowVersion = 1;
    public const int IntroStepCount = 5;

    public static IReadOnlyList<OnboardingMissionDefinition> Missions { get; } =
        new[]
        {
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.AddFirstBook,
                Icon = "📘",
                Title = "Add your first book",
                TitleKey = "GettingStarted_Mission_AddFirstBook_Title",
                Description = "Create your first real library entry with title and author.",
                DescriptionKey = "GettingStarted_Mission_AddFirstBook_Description",
                CtaLabel = "Add book",
                CtaLabelKey = "GettingStarted_Mission_AddFirstBook_Cta",
                DefaultRoute = "/books/new",
                IsCore = true
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.LogFirstSession,
                Icon = "⏱️",
                Title = "Log your first reading session",
                TitleKey = "GettingStarted_Mission_LogFirstSession_Title",
                Description = "Start or finish a real reading session so BookHeart can track time, pages, XP, and streaks.",
                DescriptionKey = "GettingStarted_Mission_LogFirstSession_Description",
                CtaLabel = "Open bookshelf",
                CtaLabelKey = "GettingStarted_Mission_LogFirstSession_Cta",
                DefaultRoute = "/bookshelf",
                IsCore = true,
                Prerequisites = new[] { OnboardingMissionId.AddFirstBook }
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.RateCompletedBookAll6,
                Icon = "⭐",
                Title = "Rate a completed book in all its categories",
                TitleKey = "GettingStarted_Mission_RateBook_Title",
                Description = "Open a finished book and rate it in every category shown for its genre.",
                DescriptionKey = "GettingStarted_Mission_RateBook_Description",
                CtaLabel = "Open book",
                CtaLabelKey = "GettingStarted_Mission_RateCompletedBookAll6_Cta",
                DefaultRoute = "/bookshelf",
                IsCore = true,
                Prerequisites = new[] { OnboardingMissionId.AddFirstBook }
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.CreateFirstGoal,
                Icon = "🎯",
                Title = "Create your first goal",
                TitleKey = "GettingStarted_Mission_CreateFirstGoal_Title",
                Description = "Set up a books, pages, or minutes goal to track reading progress.",
                DescriptionKey = "GettingStarted_Mission_CreateFirstGoal_Description",
                CtaLabel = "Create goal",
                CtaLabelKey = "GettingStarted_Mission_CreateFirstGoal_Cta",
                DefaultRoute = "/goals",
                IsCore = true,
                Prerequisites = new[] { OnboardingMissionId.AddFirstBook }
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.BuyFirstPlant,
                Icon = "🪴",
                Title = "Buy your first plant",
                TitleKey = "GettingStarted_Mission_BuyFirstPlant_Title",
                Description = "Open the plant shop and purchase a real plant that can grow with your reading.",
                DescriptionKey = "GettingStarted_Mission_BuyFirstPlant_Description",
                CtaLabel = "Visit shop",
                CtaLabelKey = "GettingStarted_Mission_BuyFirstPlant_Cta",
                DefaultRoute = "/shop",
                IsCore = true,
                Prerequisites = new[] { OnboardingMissionId.LogFirstSession }
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.PlaceFirstPlantOnShelf,
                Icon = "🧺",
                Title = "Place a plant on your shelf",
                TitleKey = "GettingStarted_Mission_PlaceFirstPlantOnShelf_Title",
                Description = "Decorate your bookshelf by placing an owned plant onto a shelf slot.",
                DescriptionKey = "GettingStarted_Mission_PlaceFirstPlantOnShelf_Description",
                CtaLabel = "Open bookshelf",
                CtaLabelKey = "GettingStarted_Mission_PlaceFirstPlantOnShelf_Cta",
                DefaultRoute = "/bookshelf",
                IsCore = true,
                Prerequisites = new[] { OnboardingMissionId.BuyFirstPlant }
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.WaterFirstPlant,
                Icon = "💧",
                Title = "Water your first plant",
                TitleKey = "GettingStarted_Mission_WaterFirstPlant_Title",
                Description = "Use the real watering action after your plant has been placed on the shelf.",
                DescriptionKey = "GettingStarted_Mission_WaterFirstPlant_Description",
                CtaLabel = "Open dashboard",
                CtaLabelKey = "GettingStarted_Mission_WaterFirstPlant_Cta",
                DefaultRoute = "/dashboard",
                IsCore = true,
                IsTimeGated = true,
                Prerequisites = new[] { OnboardingMissionId.PlaceFirstPlantOnShelf }
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.ShareStatsCard,
                Icon = "📤",
                Title = "Share a stats card",
                TitleKey = "GettingStarted_Mission_ShareStatsCard_Title",
                Description = "Generate and share a real reading stats card from the Stats page.",
                DescriptionKey = "GettingStarted_Mission_ShareStatsCard_Description",
                CtaLabel = "Open stats",
                CtaLabelKey = "GettingStarted_Mission_ShareStatsCard_Cta",
                DefaultRoute = "/stats",
                Prerequisites = new[] { OnboardingMissionId.LogFirstSession }
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.ShareCompletedBook,
                Icon = "🖼️",
                Title = "Share a completed book",
                TitleKey = "GettingStarted_Mission_ShareCompletedBook_Title",
                Description = "Create and share a real recommendation card for a completed book.",
                DescriptionKey = "GettingStarted_Mission_ShareCompletedBook_Description",
                CtaLabel = "Open completed book",
                CtaLabelKey = "GettingStarted_Mission_ShareCompletedBook_Cta",
                DefaultRoute = "/bookshelf",
                Prerequisites = new[] { OnboardingMissionId.RateCompletedBookAll6 }
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.ScanIsbn,
                Icon = "📷",
                Title = "Scan an ISBN",
                TitleKey = "GettingStarted_Mission_ScanIsbn_Title",
                Description = "Use the barcode scanner to capture an ISBN from the add-book or wishlist flow.",
                DescriptionKey = "GettingStarted_Mission_ScanIsbn_Description",
                CtaLabel = "Open scanner",
                CtaLabelKey = "GettingStarted_Mission_ScanIsbn_Cta",
                DefaultRoute = "/books/new"
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.AddToWishlist,
                Icon = "💝",
                Title = "Add something to your wishlist",
                TitleKey = "GettingStarted_Mission_AddToWishlist_Title",
                Description = "Save a future read with priority and optional recommendation notes.",
                DescriptionKey = "GettingStarted_Mission_AddToWishlist_Description",
                CtaLabel = "Open wishlist",
                CtaLabelKey = "GettingStarted_Mission_AddToWishlist_Cta",
                DefaultRoute = "/bookshelf"
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.CreateBackup,
                Icon = "☁️",
                Title = "Create your first backup",
                TitleKey = "GettingStarted_Mission_CreateBackup_Title",
                Description = "Create a real cloud/share backup from Settings.",
                DescriptionKey = "GettingStarted_Mission_CreateBackup_Description",
                CtaLabel = "Open settings",
                CtaLabelKey = "GettingStarted_Mission_CreateBackup_Cta",
                DefaultRoute = "/settings",
                Prerequisites = new[] { OnboardingMissionId.AddFirstBook }
            }
        };

    public static IReadOnlyList<OnboardingFeatureAtlasEntry> FeatureAtlas { get; } =
        new[]
        {
            new OnboardingFeatureAtlasEntry
            {
                Icon = "🔔",
                Title = "Notifications and reminders",
                TitleKey = "GettingStarted_Atlas_Notifications_Title",
                Description = "Configure reading reminders and notification permissions in Settings.",
                DescriptionKey = "GettingStarted_Atlas_Notifications_Description",
                Route = "/settings",
                CtaLabel = "Open settings",
                CtaLabelKey = "GettingStarted_Atlas_Notifications_Cta"
            },
            new OnboardingFeatureAtlasEntry
            {
                Icon = "♻️",
                Title = "Restore from backup",
                TitleKey = "GettingStarted_Atlas_RestoreBackup_Title",
                Description = "Restore is intentionally not a mission because it can overwrite data. Use it from Settings when you really need it.",
                DescriptionKey = "GettingStarted_Atlas_RestoreBackup_Description",
                Route = "/settings",
                CtaLabel = "Open settings",
                CtaLabelKey = "GettingStarted_Atlas_RestoreBackup_Cta",
                Badge = "Careful",
                BadgeKey = "GettingStarted_Atlas_RestoreBackup_Badge"
            },
            new OnboardingFeatureAtlasEntry
            {
                Icon = "🗑️",
                Title = "Delete all data",
                TitleKey = "GettingStarted_Atlas_DeleteData_Title",
                Description = "The destructive reset flow is documented here but never required for onboarding.",
                DescriptionKey = "GettingStarted_Atlas_DeleteData_Description",
                Route = "/settings",
                CtaLabel = "Open settings",
                CtaLabelKey = "GettingStarted_Atlas_DeleteData_Cta",
                Badge = "Risky",
                BadgeKey = "GettingStarted_Atlas_DeleteData_Badge"
            },
            new OnboardingFeatureAtlasEntry
            {
                Icon = "🪟",
                Title = "Android widgets",
                TitleKey = "GettingStarted_Atlas_AndroidWidgets_Title",
                Description = "Widgets are configured on the home screen, so this stays informational inside onboarding.",
                DescriptionKey = "GettingStarted_Atlas_AndroidWidgets_Description",
                Badge = "Android",
                BadgeKey = "GettingStarted_Atlas_AndroidWidgets_Badge"
            },
            new OnboardingFeatureAtlasEntry
            {
                Icon = "🆕",
                Title = "Updates and changelog",
                TitleKey = "GettingStarted_Atlas_Changelog_Title",
                Description = "BookHeart can show update prompts and changelog entries automatically when relevant.",
                DescriptionKey = "GettingStarted_Atlas_Changelog_Description",
                Badge = "Auto",
                BadgeKey = "GettingStarted_Atlas_Changelog_Badge"
            },
            new OnboardingFeatureAtlasEntry
            {
                Icon = "🔒",
                Title = "Privacy policy",
                TitleKey = "GettingStarted_Atlas_Privacy_Title",
                Description = "Review the privacy policy directly in Settings.",
                DescriptionKey = "GettingStarted_Atlas_Privacy_Description",
                Route = "/settings",
                CtaLabel = "Open settings",
                CtaLabelKey = "GettingStarted_Atlas_Privacy_Cta"
            },
            new OnboardingFeatureAtlasEntry
            {
                Icon = "🛟",
                Title = "Support and diagnostics",
                TitleKey = "GettingStarted_Atlas_Support_Title",
                Description = "Access support links and migration diagnostics from Settings.",
                DescriptionKey = "GettingStarted_Atlas_Support_Description",
                Route = "/settings",
                CtaLabel = "Open settings",
                CtaLabelKey = "GettingStarted_Atlas_Support_Cta"
            },
            new OnboardingFeatureAtlasEntry
            {
                Icon = "❤️",
                Title = "Review prompts",
                TitleKey = "GettingStarted_Atlas_ReviewPrompts_Title",
                Description = "The review prompt appears at appropriate in-app moments and is intentionally not forced.",
                DescriptionKey = "GettingStarted_Atlas_ReviewPrompts_Description",
                Badge = "Contextual",
                BadgeKey = "GettingStarted_Atlas_ReviewPrompts_Badge"
            }
        };

    public static OnboardingMissionDefinition GetDefinition(OnboardingMissionId missionId) =>
        Missions.First(m => m.Id == missionId);
}
