using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data.SeedData;

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
    public DbSet<Trope> Tropes => Set<Trope>();
    public DbSet<BookTrope> BookTropes => Set<BookTrope>();
    public DbSet<WishlistInfo> WishlistInfos => Set<WishlistInfo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Many-to-Many for Book <-> Shelf
        modelBuilder.Entity<BookShelf>()
            .HasKey(bs => new { bs.BookId, bs.ShelfId });

        // Configure Many-to-Many for Plant <-> Shelf
        modelBuilder.Entity<PlantShelf>()
            .HasKey(ps => new { ps.PlantId, ps.ShelfId });

        // Configure Many-to-Many for Book <-> Trope
        modelBuilder.Entity<BookTrope>()
            .HasKey(bt => new { bt.BookId, bt.TropeId });

        modelBuilder.Entity<BookTrope>()
            .HasOne(bt => bt.Book)
            .WithMany(b => b.BookTropes)
            .HasForeignKey(bt => bt.BookId);

        modelBuilder.Entity<BookTrope>()
            .HasOne(bt => bt.Trope)
            .WithMany(t => t.BookTropes)
            .HasForeignKey(bt => bt.TropeId);

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
        modelBuilder.Entity<PlantSpecies>().HasData(PlantSeedData.GetPlants());

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

        // Seed Tropes
        modelBuilder.Entity<Trope>().HasData(TropeSeedData.GetTropes());
    }
}
