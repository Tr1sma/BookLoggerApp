namespace BookLoggerApp.Core.Models;

public enum OnboardingEvent
{
    None = 0,
    BookCreated = 1,
    ReadingSessionLogged = 2,
    BookRated = 3,
    GoalCreated = 4,
    PlantPurchased = 5,
    PlantPlacedOnShelf = 6,
    PlantWatered = 7,
    StatsShared = 8,
    BookShared = 9,
    IsbnScanned = 10,
    WishlistAdded = 11,
    BackupCreated = 12,
    IntroCompleted = 13,
    IntroSkipped = 14
}
