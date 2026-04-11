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
                Description = "Create your first real library entry with title and author.",
                CtaLabel = "Add book",
                DefaultRoute = "/books/new",
                IsCore = true
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.LogFirstSession,
                Icon = "⏱️",
                Title = "Log your first reading session",
                Description = "Start or finish a real reading session so BookHeart can track time, pages, XP, and streaks.",
                CtaLabel = "Open bookshelf",
                DefaultRoute = "/bookshelf",
                IsCore = true,
                Prerequisites = new[] { OnboardingMissionId.AddFirstBook }
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.RateCompletedBookAll6,
                Icon = "⭐",
                Title = "Rate a completed book in all 6 categories",
                Description = "Use the real rating system: Characters, Plot, Writing Style, Spice Level, Pacing, and World Building.",
                CtaLabel = "Open book",
                DefaultRoute = "/bookshelf",
                IsCore = true,
                Prerequisites = new[] { OnboardingMissionId.AddFirstBook }
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.CreateFirstGoal,
                Icon = "🎯",
                Title = "Create your first goal",
                Description = "Set up a books, pages, or minutes goal to track reading progress.",
                CtaLabel = "Create goal",
                DefaultRoute = "/goals",
                IsCore = true,
                Prerequisites = new[] { OnboardingMissionId.AddFirstBook }
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.BuyFirstPlant,
                Icon = "🪴",
                Title = "Buy your first plant",
                Description = "Open the plant shop and purchase a real plant that can grow with your reading.",
                CtaLabel = "Visit shop",
                DefaultRoute = "/shop",
                IsCore = true,
                Prerequisites = new[] { OnboardingMissionId.LogFirstSession }
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.PlaceFirstPlantOnShelf,
                Icon = "🧺",
                Title = "Place a plant on your shelf",
                Description = "Decorate your bookshelf by placing an owned plant onto a shelf slot.",
                CtaLabel = "Open bookshelf",
                DefaultRoute = "/bookshelf",
                IsCore = true,
                Prerequisites = new[] { OnboardingMissionId.BuyFirstPlant }
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.WaterFirstPlant,
                Icon = "💧",
                Title = "Water your first plant",
                Description = "Use the real watering action after your plant has been placed on the shelf.",
                CtaLabel = "Open dashboard",
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
                Description = "Generate and share a real reading stats card from the Stats page.",
                CtaLabel = "Open stats",
                DefaultRoute = "/stats",
                Prerequisites = new[] { OnboardingMissionId.LogFirstSession }
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.ShareCompletedBook,
                Icon = "🖼️",
                Title = "Share a completed book",
                Description = "Create and share a real recommendation card for a completed book.",
                CtaLabel = "Open completed book",
                DefaultRoute = "/bookshelf",
                Prerequisites = new[] { OnboardingMissionId.RateCompletedBookAll6 }
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.ScanIsbn,
                Icon = "📷",
                Title = "Scan an ISBN",
                Description = "Use the barcode scanner to capture an ISBN from the add-book or wishlist flow.",
                CtaLabel = "Open scanner",
                DefaultRoute = "/books/new"
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.AddToWishlist,
                Icon = "💝",
                Title = "Add something to your wishlist",
                Description = "Save a future read with priority and optional recommendation notes.",
                CtaLabel = "Open wishlist",
                DefaultRoute = "/bookshelf"
            },
            new OnboardingMissionDefinition
            {
                Id = OnboardingMissionId.CreateBackup,
                Icon = "☁️",
                Title = "Create your first backup",
                Description = "Create a real cloud/share backup from Settings.",
                CtaLabel = "Open settings",
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
                Description = "Configure reading reminders and notification permissions in Settings.",
                Route = "/settings",
                CtaLabel = "Open settings"
            },
            new OnboardingFeatureAtlasEntry
            {
                Icon = "♻️",
                Title = "Restore from backup",
                Description = "Restore is intentionally not a mission because it can overwrite data. Use it from Settings when you really need it.",
                Route = "/settings",
                CtaLabel = "Open settings",
                Badge = "Careful"
            },
            new OnboardingFeatureAtlasEntry
            {
                Icon = "🗑️",
                Title = "Delete all data",
                Description = "The destructive reset flow is documented here but never required for onboarding.",
                Route = "/settings",
                CtaLabel = "Open settings",
                Badge = "Risky"
            },
            new OnboardingFeatureAtlasEntry
            {
                Icon = "🪟",
                Title = "Android widgets",
                Description = "Widgets are configured on the home screen, so this stays informational inside onboarding.",
                Badge = "Android"
            },
            new OnboardingFeatureAtlasEntry
            {
                Icon = "🆕",
                Title = "Updates and changelog",
                Description = "BookHeart can show update prompts and changelog entries automatically when relevant.",
                Badge = "Auto"
            },
            new OnboardingFeatureAtlasEntry
            {
                Icon = "🔒",
                Title = "Privacy policy",
                Description = "Review the privacy policy directly in Settings.",
                Route = "/settings",
                CtaLabel = "Open settings"
            },
            new OnboardingFeatureAtlasEntry
            {
                Icon = "🛟",
                Title = "Support and diagnostics",
                Description = "Access support links and migration diagnostics from Settings.",
                Route = "/settings",
                CtaLabel = "Open settings"
            },
            new OnboardingFeatureAtlasEntry
            {
                Icon = "❤️",
                Title = "Review prompts",
                Description = "The review prompt appears at appropriate in-app moments and is intentionally not forced.",
                Badge = "Contextual"
            }
        };

    public static OnboardingMissionDefinition GetDefinition(OnboardingMissionId missionId) =>
        Missions.First(m => m.Id == missionId);
}
