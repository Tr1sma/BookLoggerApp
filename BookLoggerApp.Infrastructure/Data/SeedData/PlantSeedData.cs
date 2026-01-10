using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.SeedData;

/// <summary>
/// Central source of truth for plant species data.
/// Used by AppDbContext for seeding (migrations) and DbInitializer for runtime syncing.
/// </summary>
public static class PlantSeedData
{
    private static readonly Guid _starterSproutId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid _bookwormFernId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid _readingCactusId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid _storySeedlingId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid _literaryLilyId = Guid.Parse("10000000-0000-0000-0000-000000000005");
    private static readonly Guid _wisdomWillowId = Guid.Parse("10000000-0000-0000-0000-000000000006");
    private static readonly Guid _ancientBonsaiId = Guid.Parse("10000000-0000-0000-0000-000000000007");
    private static readonly Guid _mysticTomeTreeId = Guid.Parse("10000000-0000-0000-0000-000000000008");

    public static IEnumerable<PlantSpecies> GetPlants()
    {
        yield return new PlantSpecies
        {
            Id = _starterSproutId,
            Name = "Starter Sprout",
            Description = "A simple plant for beginners. Grows quickly!",
            ImagePath = "images/plants/starter_sprout.svg",
            MaxLevel = 10,
            WaterIntervalDays = 3,
            GrowthRate = 1.2,
            XpBoostPercentage = 0.05m,
            BaseCost = 500,
            UnlockLevel = 1,
            IsAvailable = true
        };

        yield return new PlantSpecies
        {
            Id = _storySeedlingId,
            Name = "Story Seedling",
            Description = "A growing seedling nurtured by stories.",
            ImagePath = "images/plants/story_seedling.svg",
            MaxLevel = 11,
            WaterIntervalDays = 4,
            GrowthRate = 1.1,
            XpBoostPercentage = 0.06m,
            BaseCost = 600,
            UnlockLevel = 3,
            IsAvailable = true
        };

        yield return new PlantSpecies
        {
            Id = _bookwormFernId,
            Name = "Bookworm Fern",
            Description = "A lush fern for dedicated readers.",
            ImagePath = "images/plants/bookworm_fern.svg",
            MaxLevel = 12,
            WaterIntervalDays = 4,
            GrowthRate = 1.0,
            XpBoostPercentage = 0.08m,
            BaseCost = 750,
            UnlockLevel = 8,
            IsAvailable = true
        };

        yield return new PlantSpecies
        {
            Id = _literaryLilyId,
            Name = "Literary Lily",
            Description = "A beautiful lily that blooms with every chapter.",
            ImagePath = "images/plants/literary_lily.svg",
            MaxLevel = 14,
            WaterIntervalDays = 5,
            GrowthRate = 0.9,
            XpBoostPercentage = 0.09m,
            BaseCost = 850,
            UnlockLevel = 14,
            IsAvailable = true
        };

        yield return new PlantSpecies
        {
            Id = _readingCactusId,
            Name = "Reading Cactus",
            Description = "Low maintenance, high rewards.",
            ImagePath = "images/plants/reading_cactus.svg",
            MaxLevel = 15,
            WaterIntervalDays = 7,
            GrowthRate = 0.8,
            XpBoostPercentage = 0.10m,
            BaseCost = 1000,
            UnlockLevel = 21,
            IsAvailable = true
        };

        yield return new PlantSpecies
        {
            Id = _wisdomWillowId,
            Name = "Wisdom Willow",
            Description = "A wise tree that stands the test of time.",
            ImagePath = "images/plants/wisdom_willow.svg",
            MaxLevel = 18,
            WaterIntervalDays = 8,
            GrowthRate = 0.7,
            XpBoostPercentage = 0.12m,
            BaseCost = 1500,
            UnlockLevel = 28,
            IsAvailable = true
        };

        yield return new PlantSpecies
        {
            Id = _ancientBonsaiId,
            Name = "Ancient Knowledge Bonsai",
            Description = "An ancient bonsai radiating knowledge.",
            ImagePath = "images/plants/ancient_bonsai.svg",
            MaxLevel = 20,
            WaterIntervalDays = 10,
            GrowthRate = 0.6,
            XpBoostPercentage = 0.15m,
            BaseCost = 2500,
            UnlockLevel = 31,
            IsAvailable = true
        };

        yield return new PlantSpecies
        {
            Id = _mysticTomeTreeId,
            Name = "Mystic Tome Tree",
            Description = "A legendary tree with leaves like parchment.",
            ImagePath = "images/plants/mystic_tome_tree.svg",
            MaxLevel = 25,
            WaterIntervalDays = 14,
            GrowthRate = 0.5,
            XpBoostPercentage = 0.20m,
            BaseCost = 5000,
            UnlockLevel = 32,
            IsAvailable = true
        };
    }
}
