using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data.SeedData;

namespace BookLoggerApp.Infrastructure.Data;

/// <summary>Main database context for BookLoggerApp.</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Book> Books => Set<Book>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<BookGenre> BookGenres => Set<BookGenre>();
    public DbSet<ReadingSession> ReadingSessions => Set<ReadingSession>();
    public DbSet<ReadingSessionMood> ReadingSessionMoods => Set<ReadingSessionMood>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Annotation> Annotations => Set<Annotation>();
    public DbSet<ReadingGoal> ReadingGoals => Set<ReadingGoal>();
    public DbSet<PlantSpecies> PlantSpecies => Set<PlantSpecies>();
    public DbSet<UserPlant> UserPlants => Set<UserPlant>();
    public DbSet<ShopItem> ShopItems => Set<ShopItem>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<OnboardingMissionState> OnboardingMissionStates => Set<OnboardingMissionState>();
    public DbSet<BookRatingSummary> BookRatingSummaries => Set<BookRatingSummary>(); // View
    public DbSet<Shelf> Shelves => Set<Shelf>();
    public DbSet<BookShelf> BookShelves => Set<BookShelf>();
    public DbSet<PlantShelf> PlantShelves => Set<PlantShelf>();
    public DbSet<UserDecoration> UserDecorations => Set<UserDecoration>();
    public DbSet<DecorationShelf> DecorationShelves => Set<DecorationShelf>();
    public DbSet<Trope> Tropes => Set<Trope>();
    public DbSet<BookTrope> BookTropes => Set<BookTrope>();
    public DbSet<WishlistInfo> WishlistInfos => Set<WishlistInfo>();
    public DbSet<GoalExcludedBook> GoalExcludedBooks => Set<GoalExcludedBook>();
    public DbSet<GoalGenre> GoalGenres => Set<GoalGenre>();
    public DbSet<UserEntitlement> UserEntitlements => Set<UserEntitlement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Many-to-Many for Book <-> Shelf
        modelBuilder.Entity<BookShelf>()
            .HasKey(bs => new { bs.BookId, bs.ShelfId });

        // Configure Many-to-Many for Plant <-> Shelf
        modelBuilder.Entity<PlantShelf>()
            .HasKey(ps => new { ps.PlantId, ps.ShelfId });

        // Configure Many-to-Many for Decoration <-> Shelf
        modelBuilder.Entity<DecorationShelf>()
            .HasKey(ds => new { ds.DecorationId, ds.ShelfId });

        // Configure Many-to-Many for ReadingGoal <-> Book (excluded books)
        modelBuilder.Entity<GoalExcludedBook>()
            .HasKey(ge => new { ge.ReadingGoalId, ge.BookId });

        modelBuilder.Entity<GoalExcludedBook>()
            .HasOne(ge => ge.ReadingGoal)
            .WithMany(rg => rg.ExcludedBooks)
            .HasForeignKey(ge => ge.ReadingGoalId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GoalExcludedBook>()
            .HasOne(ge => ge.Book)
            .WithMany()
            .HasForeignKey(ge => ge.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Many-to-Many for ReadingGoal <-> Genre (goal genre filter)
        modelBuilder.Entity<GoalGenre>()
            .HasKey(gg => new { gg.ReadingGoalId, gg.GenreId });

        modelBuilder.Entity<GoalGenre>()
            .HasOne(gg => gg.ReadingGoal)
            .WithMany(rg => rg.GoalGenres)
            .HasForeignKey(gg => gg.ReadingGoalId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GoalGenre>()
            .HasOne(gg => gg.Genre)
            .WithMany()
            .HasForeignKey(gg => gg.GenreId)
            .OnDelete(DeleteBehavior.Cascade);

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

        // BookRatingSummary is a keyless view.
        modelBuilder.Entity<BookRatingSummary>().HasNoKey();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Optimistic-concurrency tokens. SQLite has no native rowversion, so [Timestamp] is a no-op.
        // For entities where lost updates matter (AppSettings, UserEntitlement) we make RowVersion a
        // real app-set token (ValueGeneratedNever + IsConcurrencyToken, stamped in StampRowVersions).
        // Others carry RowVersion but are updated via the repository's blind-update path, so we demote
        // theirs to a non-token column (last-writer-wins).
        var concurrencyEntities = new[] { typeof(AppSettings), typeof(UserEntitlement) };
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersion = entityType.FindProperty("RowVersion");
            if (rowVersion is null || rowVersion.ClrType != typeof(byte[]))
                continue;

            modelBuilder.Entity(entityType.ClrType)
                .Property("RowVersion")
                .ValueGeneratedNever()
                .IsConcurrencyToken(Array.IndexOf(concurrencyEntities, entityType.ClrType) >= 0);
        }

        // Seed data
        SeedData(modelBuilder);
    }

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Stamps a fresh RowVersion on every added/modified entity that has a token; the in-app substitute for SQLite's missing native rowversion.</summary>
    private void StampRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added && entry.State != EntityState.Modified)
                continue;

            var rowVersion = entry.Metadata.FindProperty("RowVersion");
            if (rowVersion is null || rowVersion.ClrType != typeof(byte[]) || !rowVersion.IsConcurrencyToken)
                continue;

            entry.Property("RowVersion").CurrentValue = Guid.NewGuid().ToByteArray();
        }
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
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

        modelBuilder.Entity<PlantSpecies>().HasData(PlantSeedData.GetPlants());
        modelBuilder.Entity<ShopItem>().HasData(DecorationSeedData.GetDecorations());

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
                HasCompletedOnboarding = false,
                OnboardingFlowVersion = OnboardingMissionCatalog.CurrentFlowVersion,
                OnboardingIntroStatus = OnboardingIntroStatus.NotStarted,
                OnboardingCurrentStep = 0,
                OnboardingAutoCompletedForExistingUser = false,
                OnboardingTutorialPlantNeedsWateringAssist = false,
                ShelfLedgeColor = "#8B7355",
                ShelfBaseColor = "#D4A574",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        modelBuilder.Entity<Trope>().HasData(TropeSeedData.GetTropes());

        // Single-row default: everyone starts Free.
        modelBuilder.Entity<UserEntitlement>().HasData(
            new UserEntitlement
            {
                Id = Guid.Parse("99999999-0000-0000-0000-000000000002"),
                Tier = BookLoggerApp.Core.Entitlements.SubscriptionTier.Free,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
