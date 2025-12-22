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
    public DbSet<BookRatingSummary> BookRatingSummaries => Set<BookRatingSummary>(); // View
    public DbSet<Shelf> Shelves => Set<Shelf>();
    public DbSet<BookShelf> BookShelves => Set<BookShelf>();
    public DbSet<PlantShelf> PlantShelves => Set<PlantShelf>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Many-to-Many for Book <-> Shelf
        modelBuilder.Entity<BookShelf>()
            .HasKey(bs => new { bs.BookId, bs.ShelfId });

        // Configure Many-to-Many for Plant <-> Shelf
        modelBuilder.Entity<PlantShelf>()
            .HasKey(ps => new { ps.PlantId, ps.ShelfId });

        // Configure BookRatingSummary as Keyless (View)
        modelBuilder.Entity<BookRatingSummary>().HasNoKey();

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
            History = Guid.Parse("00000000-0000-0000-0000-000000000008"),
            DarkRomance = Guid.Parse("00000000-0000-0000-0000-000000000009"),
            Krimi = Guid.Parse("00000000-0000-0000-0000-000000000010"),
            Comedy = Guid.Parse("00000000-0000-0000-0000-000000000011"),
            Thriller = Guid.Parse("00000000-0000-0000-0000-000000000012")
        };

        modelBuilder.Entity<Genre>().HasData(
            new Genre { Id = genreIds.Fiction, Name = "Fiction", Icon = "📖", ColorHex = "#3498db" },
            new Genre { Id = genreIds.NonFiction, Name = "Non-Fiction", Icon = "📚", ColorHex = "#2ecc71" },
            new Genre { Id = genreIds.Fantasy, Name = "Fantasy", Icon = "🧙", ColorHex = "#9b59b6" },
            new Genre { Id = genreIds.SciFi, Name = "Science Fiction", Icon = "🚀", ColorHex = "#1abc9c" },
            new Genre { Id = genreIds.Mystery, Name = "Mystery", Icon = "🔍", ColorHex = "#e74c3c" },
            new Genre { Id = genreIds.Romance, Name = "Romance", Icon = "💕", ColorHex = "#e91e63" },
            new Genre { Id = genreIds.Biography, Name = "Biography", Icon = "👤", ColorHex = "#f39c12" },
            new Genre { Id = genreIds.History, Name = "History", Icon = "📜", ColorHex = "#95a5a6" },
            new Genre { Id = genreIds.DarkRomance, Name = "Dark Romance", Icon = "🖤", ColorHex = "#880e4f" },
            new Genre { Id = genreIds.Krimi, Name = "Krimi", Icon = "🔦", ColorHex = "#34495e" },
            new Genre { Id = genreIds.Comedy, Name = "Comedy", Icon = "🎭", ColorHex = "#f1c40f" },
            new Genre { Id = genreIds.Thriller, Name = "Thriller", Icon = "😱", ColorHex = "#c0392b" }
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
                XpBoostPercentage = 0.05m, // 5% base boost, +0.5% per level (max 10% at level 10)
                BaseCost = 500,
                UnlockLevel = 1,
                IsAvailable = true
            },
            new PlantSpecies
            {
                Id = plantIds.BookwormFern,
                Name = "Bookworm Fern",
                Description = "A lush fern for dedicated readers.",
                ImagePath = "images/plants/bookworm_fern.svg",
                MaxLevel = 12,
                WaterIntervalDays = 4,
                GrowthRate = 1.0,
                XpBoostPercentage = 0.08m, // 8% base boost, +0.5% per level (max 14% at level 12)
                BaseCost = 750,
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
                XpBoostPercentage = 0.10m, // 10% base boost, +1% per level (max 25% at level 15)
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
                PlantsPurchased = 0, // Counter for dynamic plant pricing
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
