using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data;

/// <summary>
/// Main database context for BookLoggerApp.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<BookGenre> BookGenres => Set<BookGenre>();
    public DbSet<ReadingSession> ReadingSessions => Set<ReadingSession>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Annotation> Annotations => Set<Annotation>();
    public DbSet<ReadingGoal> ReadingGoals => Set<ReadingGoal>();
    public DbSet<PlantSpecies> PlantSpecies => Set<PlantSpecies>();
    public DbSet<UserPlant> UserPlants => Set<UserPlant>();
    public DbSet<ShopItem> ShopItems => Set<ShopItem>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Seed data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Seed Genres
        var genreIds = new
        {
            Fiction = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            NonFiction = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Fantasy = Guid.Parse("00000000-0000-0000-0000-000000000003"),
            SciFi = Guid.Parse("00000000-0000-0000-0000-000000000004"),
            Mystery = Guid.Parse("00000000-0000-0000-0000-000000000005"),
            Romance = Guid.Parse("00000000-0000-0000-0000-000000000006"),
            Biography = Guid.Parse("00000000-0000-0000-0000-000000000007"),
            History = Guid.Parse("00000000-0000-0000-0000-000000000008")
        };

        modelBuilder.Entity<Genre>().HasData(
            new Genre { Id = genreIds.Fiction, Name = "Fiction", Icon = "📖", ColorHex = "#3498db" },
            new Genre { Id = genreIds.NonFiction, Name = "Non-Fiction", Icon = "📚", ColorHex = "#2ecc71" },
            new Genre { Id = genreIds.Fantasy, Name = "Fantasy", Icon = "🧙", ColorHex = "#9b59b6" },
            new Genre { Id = genreIds.SciFi, Name = "Science Fiction", Icon = "🚀", ColorHex = "#1abc9c" },
            new Genre { Id = genreIds.Mystery, Name = "Mystery", Icon = "🔍", ColorHex = "#e74c3c" },
            new Genre { Id = genreIds.Romance, Name = "Romance", Icon = "💕", ColorHex = "#e91e63" },
            new Genre { Id = genreIds.Biography, Name = "Biography", Icon = "👤", ColorHex = "#f39c12" },
            new Genre { Id = genreIds.History, Name = "History", Icon = "📜", ColorHex = "#95a5a6" }
        );

        // Seed PlantSpecies
        var plantIds = new
        {
            StarterSprout = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            BookwormFern = Guid.Parse("10000000-0000-0000-0000-000000000002"),
            ReadingCactus = Guid.Parse("10000000-0000-0000-0000-000000000003")
        };

        modelBuilder.Entity<PlantSpecies>().HasData(
            new PlantSpecies
            {
                Id = plantIds.StarterSprout,
                Name = "Starter Sprout",
                Description = "A simple plant for beginners. Grows quickly!",
                ImagePath = "images/plants/starter_sprout.svg",
                MaxLevel = 10,
                WaterIntervalDays = 3,
                GrowthRate = 1.2,
                BaseCost = 500,
                UnlockLevel = 5,
                IsAvailable = true
            },
            new PlantSpecies
            {
                Id = plantIds.ReadingCactus,
                Name = "Reading Cactus",
                Description = "Low maintenance, high rewards.",
                ImagePath = "images/plants/reading_cactus.svg",
                MaxLevel = 15,
                WaterIntervalDays = 7,
                GrowthRate = 0.8,
                BaseCost = 1000,
                UnlockLevel = 10,
                IsAvailable = true
            }
        );

        // Seed AppSettings (default)
        modelBuilder.Entity<AppSettings>().HasData(
            new AppSettings
            {
                Id = Guid.Parse("99999999-0000-0000-0000-000000000001"),
                Theme = "Light",
                Language = "en",
                NotificationsEnabled = false,
                ReadingRemindersEnabled = false,
                AutoBackupEnabled = false,
                TelemetryEnabled = false,
                UserLevel = 1,
                TotalXp = 0,
                Coins = 100, // Starting coins
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
