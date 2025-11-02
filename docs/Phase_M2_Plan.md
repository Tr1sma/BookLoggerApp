# Phase M2: EF Core Migration & Datenmodell

**Zeitrahmen:** 2 Wochen (80 Stunden)
**Status:** Planned
**Dependencies:** M1 (Basis-Implementation)
**Blocks:** M3 (Services benötigen DbContext)

---

## Überblick

In dieser Phase migrieren wir von sqlite-net-pcl zu **Entity Framework Core SQLite** und erweitern das Datenmodell von 2 auf 12+ Entitäten. Dies schafft die Grundlage für alle weiteren Features.

### Ziele

1. ✅ Migration von sqlite-net-pcl zu EF Core
2. ✅ AppDbContext mit allen Konfigurationen erstellen
3. ✅ 12 Domain Models implementieren
4. ✅ Code-First Migrations einrichten
5. ✅ Bestehende Daten migrieren (falls vorhanden)
6. ✅ Repository Pattern implementieren
7. ✅ Unit Tests für alle Models & Repositories

### Deliverables

- `BookLoggerApp.Infrastructure` Projekt erstellt
- `AppDbContext.cs` mit allen DbSets
- 12 Model-Klassen erweitert/neu erstellt
- EF Core Configurations für alle Entitäten
- Initial Migration generiert
- Datenmigrations-Script (für bestehende DBs)
- Repository Pattern implementiert
- Unit Tests (≥80% Coverage)

---

## Arbeitspaket 1: Projektstruktur & EF Core Setup

**Aufwand:** 8 Stunden
**Priorität:** P0 (Blocker)

### Tasks

#### 1.1 Infrastructure-Projekt erstellen
```bash
# Neues Class Library Projekt
dotnet new classlib -n BookLoggerApp.Infrastructure -f net9.0

# Zum Solution hinzufügen
dotnet sln add BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj

# Referenz von Core zu Infrastructure
dotnet add BookLoggerApp.Core/BookLoggerApp.Core.csproj reference BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj

# Referenz von MAUI App zu Infrastructure
dotnet add BookLoggerApp/BookLoggerApp.csproj reference BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj
```

#### 1.2 NuGet Packages installieren

**Infrastructure Projekt:**
```bash
cd BookLoggerApp.Infrastructure

# EF Core SQLite
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.0

# EF Core Tools (für Migrations)
dotnet add package Microsoft.EntityFrameworkCore.Design --version 9.0.0

# EF Core Abstractions
dotnet add package Microsoft.EntityFrameworkCore.Abstractions --version 9.0.0
```

**Core Projekt:**
```bash
cd BookLoggerApp.Core

# EF Core Annotations (für Models)
dotnet add package Microsoft.EntityFrameworkCore --version 9.0.0
```

#### 1.3 Ordnerstruktur erstellen

```
BookLoggerApp.Infrastructure/
├── Data/
│   ├── AppDbContext.cs
│   ├── Configurations/          # Fluent API Configurations
│   │   ├── BookConfiguration.cs
│   │   ├── GenreConfiguration.cs
│   │   ├── ReadingSessionConfiguration.cs
│   │   └── ... (für jede Entität)
│   └── Migrations/              # Auto-generiert
│       └── (EF Core Migrations)
├── Repositories/
│   ├── IRepository.cs           # Generic Repository Interface
│   ├── Repository.cs            # Generic Repository Implementation
│   └── Specific/                # Spezielle Repositories (falls nötig)
│       ├── IBookRepository.cs
│       └── BookRepository.cs
└── Services/                    # Service Implementations (später in M3)
```

#### 1.4 sqlite-net-pcl Package aus Core entfernen

```bash
cd BookLoggerApp.Core
dotnet remove package sqlite-net-pcl
dotnet remove package SQLitePCLRaw.bundle_green
```

**Achtung:** Bestehende Services werden temporär brechen → in M3 fixen

### Acceptance Criteria

- [ ] Infrastructure-Projekt existiert und kompiliert
- [ ] EF Core Packages installiert (Sqlite, Design)
- [ ] Ordnerstruktur angelegt
- [ ] sqlite-net-pcl entfernt (Breaking Changes OK für M2)

---

## Arbeitspaket 2: Domain Models erweitern

**Aufwand:** 16 Stunden
**Priorität:** P0 (Blocker)

### Bestehende Models erweitern

#### 2.1 Book.cs erweitern

**Location:** `BookLoggerApp.Core/Models/Book.cs`

**Aktuell (M1):**
```csharp
public sealed class Book
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public ReadingStatus Status { get; set; } = ReadingStatus.Planned;
}
```

**Neu (M2):**
```csharp
using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a book in the user's library.
/// </summary>
public class Book
{
    // Primary Key
    public Guid Id { get; set; } = Guid.NewGuid();

    // Basic Info
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Author { get; set; } = string.Empty;

    [MaxLength(13)]
    public string? ISBN { get; set; }

    [MaxLength(200)]
    public string? Publisher { get; set; }

    public int? PublicationYear { get; set; }

    [MaxLength(50)]
    public string? Language { get; set; }

    // Content
    [MaxLength(2000)]
    public string? Description { get; set; }

    public int? PageCount { get; set; }

    public int CurrentPage { get; set; } = 0;

    // Media
    [MaxLength(500)]
    public string? CoverImagePath { get; set; }

    // Status & Rating
    public ReadingStatus Status { get; set; } = ReadingStatus.Planned;

    public int? Rating { get; set; } // 1-5 stars, nullable

    // Timestamps
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    public DateTime? DateStarted { get; set; }
    public DateTime? DateCompleted { get; set; }

    // Navigation Properties
    public ICollection<BookGenre> BookGenres { get; set; } = new List<BookGenre>();
    public ICollection<ReadingSession> ReadingSessions { get; set; } = new List<ReadingSession>();
    public ICollection<Quote> Quotes { get; set; } = new List<Quote>();
    public ICollection<Annotation> Annotations { get; set; } = new List<Annotation>();
    public Rating? BookRating { get; set; } // 1:1 or nullable

    // Computed Properties
    public int ProgressPercentage => PageCount > 0 ? (CurrentPage * 100 / PageCount.Value) : 0;
}
```

#### 2.2 ReadingSession.cs erweitern

**Location:** `BookLoggerApp.Core/Models/ReadingSession.cs`

**Aktuell:**
```csharp
public sealed class ReadingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public int Minutes { get; set; } = 0;
    public int? PagesRead { get; set; }
}
```

**Neu:**
```csharp
using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a single reading session for a book.
/// </summary>
public class ReadingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!; // Navigation Property

    // Session Data
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    [Range(0, 1440)] // Max 24 hours
    public int Minutes { get; set; } = 0;

    [Range(0, 10000)]
    public int? PagesRead { get; set; }

    public int? StartPage { get; set; }
    public int? EndPage { get; set; }

    // Gamification
    public int XpEarned { get; set; } = 0; // Calculated on save

    // Notes
    [MaxLength(1000)]
    public string? Notes { get; set; }
}
```

### Neue Models erstellen

#### 2.3 Genre.cs

**Location:** `BookLoggerApp.Core/Models/Genre.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a book genre/category.
/// </summary>
public class Genre
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; } // Icon name or emoji

    [MaxLength(7)]
    public string? ColorHex { get; set; } // For UI theming

    // Navigation Properties
    public ICollection<BookGenre> BookGenres { get; set; } = new List<BookGenre>();
}
```

#### 2.4 BookGenre.cs (Junction Table)

**Location:** `BookLoggerApp.Core/Models/BookGenre.cs`

```csharp
namespace BookLoggerApp.Core.Models;

/// <summary>
/// Many-to-many relationship between Books and Genres.
/// </summary>
public class BookGenre
{
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    public Guid GenreId { get; set; }
    public Genre Genre { get; set; } = null!;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
```

#### 2.5 Rating.cs

**Location:** `BookLoggerApp.Core/Models/Rating.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a user rating for a book (optional: kann auch als Book.Rating int bleiben).
/// </summary>
public class Rating
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    // Rating Data
    [Range(1, 5)]
    public int Score { get; set; } // 1-5 stars

    [MaxLength(2000)]
    public string? ReviewText { get; set; }

    public DateTime RatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Optional: Would recommend?
    public bool? WouldRecommend { get; set; }
}
```

**Entscheidung:** Ratings als separates Entity ODER als `int? Rating` im Book?
- **Empfehlung:** Für MVP reicht `int? Rating` in Book (einfacher)
- **Erweiterung v1.1:** Rating History → separates Entity

**Für M2:** Nutzen wir `int? Rating` in Book, aber bereiten die Tabelle vor (optional).

#### 2.6 Quote.cs

**Location:** `BookLoggerApp.Core/Models/Quote.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a favorite quote from a book.
/// </summary>
public class Quote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    // Quote Data
    [Required]
    [MaxLength(2000)]
    public string Text { get; set; } = string.Empty;

    [Range(0, 10000)]
    public int? PageNumber { get; set; }

    [MaxLength(500)]
    public string? Context { get; set; } // Optional context note

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsFavorite { get; set; } = false;
}
```

#### 2.7 Annotation.cs

**Location:** `BookLoggerApp.Core/Models/Annotation.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a user note/annotation for a book.
/// </summary>
public class Annotation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key
    public Guid BookId { get; set; }
    public Book Book { get; set; } = null!;

    // Annotation Data
    [Required]
    [MaxLength(5000)]
    public string Note { get; set; } = string.Empty;

    [Range(0, 10000)]
    public int? PageNumber { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; } // Optional title

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Optional: Color tag
    [MaxLength(7)]
    public string? ColorHex { get; set; }
}
```

#### 2.8 ReadingGoal.cs

**Location:** `BookLoggerApp.Core/Models/ReadingGoal.cs`

```csharp
using System.ComponentModel.DataAnnotations;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a user reading goal (e.g., read 5 books this month).
/// </summary>
public class ReadingGoal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public GoalType Type { get; set; } // Books, Pages, Minutes

    [Range(1, 1000000)]
    public int Target { get; set; } // Target value (e.g., 5 books, 1000 pages, 600 minutes)

    public int Current { get; set; } = 0; // Current progress

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }

    // Computed Properties
    public int ProgressPercentage => Target > 0 ? (Current * 100 / Target) : 0;
    public bool IsActive => !IsCompleted && DateTime.UtcNow <= EndDate;
}
```

**Enum:** `GoalType.cs` in `BookLoggerApp.Core/Enums/`

```csharp
namespace BookLoggerApp.Core.Enums;

public enum GoalType
{
    Books = 0,      // Read X books
    Pages = 1,      // Read X pages
    Minutes = 2     // Read X minutes
}
```

#### 2.9 PlantSpecies.cs

**Location:** `BookLoggerApp.Core/Models/PlantSpecies.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a plant species available in the shop.
/// </summary>
public class PlantSpecies
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string ImagePath { get; set; } = string.Empty;

    // Growth Mechanics
    [Range(1, 100)]
    public int MaxLevel { get; set; } = 10;

    [Range(1, 365)]
    public int WaterIntervalDays { get; set; } = 3; // Needs watering every X days

    [Range(0.1, 10.0)]
    public double GrowthRate { get; set; } = 1.0; // XP multiplier for leveling

    // Shop
    [Range(0, 1000000)]
    public int BaseCost { get; set; } = 100; // Cost in coins (or XP)

    public bool IsAvailable { get; set; } = true;

    [Range(1, 100)]
    public int UnlockLevel { get; set; } = 1; // User must be level X to unlock

    // Navigation Properties
    public ICollection<UserPlant> UserPlants { get; set; } = new List<UserPlant>();
}
```

#### 2.10 UserPlant.cs

**Location:** `BookLoggerApp.Core/Models/UserPlant.cs`

```csharp
using System.ComponentModel.DataAnnotations;
using BookLoggerApp.Core.Enums;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents a plant owned by the user.
/// </summary>
public class UserPlant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key
    public Guid SpeciesId { get; set; }
    public PlantSpecies Species { get; set; } = null!;

    // User-specific data
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // User-given name

    [Range(1, 100)]
    public int CurrentLevel { get; set; } = 1;

    [Range(0, 1000000)]
    public int Experience { get; set; } = 0;

    public PlantStatus Status { get; set; } = PlantStatus.Healthy;

    public DateTime LastWatered { get; set; } = DateTime.UtcNow;
    public DateTime PlantedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true; // Currently displayed plant

    // Computed Properties
    public int ExperienceToNextLevel => GetXpForLevel(CurrentLevel + 1) - Experience;
    public bool NeedsWater => (DateTime.UtcNow - LastWatered).TotalDays > Species.WaterIntervalDays;

    private int GetXpForLevel(int level)
    {
        // Example: Exponential growth (100, 250, 500, 1000, ...)
        return (int)(100 * Math.Pow(1.5, level - 1));
    }
}
```

**Enum:** `PlantStatus.cs`

```csharp
namespace BookLoggerApp.Core.Enums;

public enum PlantStatus
{
    Healthy = 0,
    Thirsty = 1,
    Wilting = 2,
    Dead = 3
}
```

#### 2.11 ShopItem.cs

**Location:** `BookLoggerApp.Core/Models/ShopItem.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents an item available in the plant shop (plants, decorations, themes).
/// </summary>
public class ShopItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ShopItemType ItemType { get; set; } // Plant, Theme, Decoration

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(0, 1000000)]
    public int Cost { get; set; } // Cost in coins or XP

    [MaxLength(500)]
    public string ImagePath { get; set; } = string.Empty;

    public bool IsAvailable { get; set; } = true;

    [Range(1, 100)]
    public int UnlockLevel { get; set; } = 1;

    // For Plants: Reference to PlantSpecies
    public Guid? PlantSpeciesId { get; set; }
    public PlantSpecies? PlantSpecies { get; set; }
}

public enum ShopItemType
{
    Plant = 0,
    Theme = 1,
    Decoration = 2
}
```

#### 2.12 AppSettings.cs

**Location:** `BookLoggerApp.Core/Models/AppSettings.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace BookLoggerApp.Core.Models;

/// <summary>
/// Represents user app settings (single-row table).
/// </summary>
public class AppSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // UI Settings
    [MaxLength(50)]
    public string Theme { get; set; } = "Light"; // Light, Dark, Auto

    [MaxLength(10)]
    public string Language { get; set; } = "en"; // ISO 639-1 code

    // Notifications
    public bool NotificationsEnabled { get; set; } = false;
    public bool ReadingRemindersEnabled { get; set; } = false;
    public TimeSpan? ReminderTime { get; set; } // e.g., 20:00 daily

    // Backup
    public bool AutoBackupEnabled { get; set; } = false;
    public DateTime? LastBackupDate { get; set; }

    // Privacy
    public bool TelemetryEnabled { get; set; } = false;

    // Gamification
    public int UserLevel { get; set; } = 1;
    public int TotalXp { get; set; } = 0;
    public int Coins { get; set; } = 0; // Currency for shop

    // Misc
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
```

### Acceptance Criteria

- [ ] Alle 12 Models erstellt/erweitert
- [ ] Navigation Properties korrekt definiert
- [ ] Data Annotations für Validierung vorhanden
- [ ] Enums erstellt (GoalType, PlantStatus, ShopItemType)
- [ ] Models kompilieren ohne Fehler

---

## Arbeitspaket 3: AppDbContext & Configurations

**Aufwand:** 16 Stunden
**Priorität:** P0 (Blocker)

### 3.1 AppDbContext erstellen

**Location:** `BookLoggerApp.Infrastructure/Data/AppDbContext.cs`

```csharp
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

    // Optional: Rating (if used)
    // public DbSet<Rating> Ratings => Set<Rating>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Seed data (optional, kann auch später)
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Seed Genres
        var genreIds = new
        {
            Fiction = Guid.NewGuid(),
            NonFiction = Guid.NewGuid(),
            Fantasy = Guid.NewGuid(),
            SciFi = Guid.NewGuid(),
            Mystery = Guid.NewGuid(),
            Romance = Guid.NewGuid(),
            Biography = Guid.NewGuid(),
            History = Guid.NewGuid()
        };

        modelBuilder.Entity<Genre>().HasData(
            new Genre { Id = genreIds.Fiction, Name = "Fiction", Icon = "📖" },
            new Genre { Id = genreIds.NonFiction, Name = "Non-Fiction", Icon = "📚" },
            new Genre { Id = genreIds.Fantasy, Name = "Fantasy", Icon = "🧙" },
            new Genre { Id = genreIds.SciFi, Name = "Science Fiction", Icon = "🚀" },
            new Genre { Id = genreIds.Mystery, Name = "Mystery", Icon = "🔍" },
            new Genre { Id = genreIds.Romance, Name = "Romance", Icon = "💕" },
            new Genre { Id = genreIds.Biography, Name = "Biography", Icon = "👤" },
            new Genre { Id = genreIds.History, Name = "History", Icon = "📜" }
        );

        // Seed PlantSpecies
        modelBuilder.Entity<PlantSpecies>().HasData(
            new PlantSpecies
            {
                Id = Guid.NewGuid(),
                Name = "Starter Sprout",
                Description = "A simple plant for beginners.",
                ImagePath = "plants/starter_sprout.png",
                MaxLevel = 5,
                WaterIntervalDays = 2,
                GrowthRate = 1.0,
                BaseCost = 0, // Free starter plant
                UnlockLevel = 1
            },
            new PlantSpecies
            {
                Id = Guid.NewGuid(),
                Name = "Bookworm Fern",
                Description = "A lush fern that grows with every page.",
                ImagePath = "plants/bookworm_fern.png",
                MaxLevel = 10,
                WaterIntervalDays = 3,
                GrowthRate = 1.2,
                BaseCost = 500,
                UnlockLevel = 5
            },
            new PlantSpecies
            {
                Id = Guid.NewGuid(),
                Name = "Reading Cactus",
                Description = "Low maintenance, high rewards.",
                ImagePath = "plants/reading_cactus.png",
                MaxLevel = 15,
                WaterIntervalDays = 7,
                GrowthRate = 0.8,
                BaseCost = 1000,
                UnlockLevel = 10
            }
        );

        // Seed AppSettings (default)
        modelBuilder.Entity<AppSettings>().HasData(
            new AppSettings
            {
                Id = Guid.NewGuid(),
                Theme = "Light",
                Language = "en",
                NotificationsEnabled = false,
                UserLevel = 1,
                TotalXp = 0,
                Coins = 100 // Starting coins
            }
        );
    }
}
```

### 3.2 Entity Configurations (Fluent API)

**Location:** `BookLoggerApp.Infrastructure/Data/Configurations/`

#### BookConfiguration.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(b => b.Author)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(b => b.ISBN)
            .HasMaxLength(13);

        builder.HasIndex(b => b.ISBN);
        builder.HasIndex(b => b.Title);
        builder.HasIndex(b => b.Status);

        // Relationships
        builder.HasMany(b => b.ReadingSessions)
            .WithOne(rs => rs.Book)
            .HasForeignKey(rs => rs.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Quotes)
            .WithOne(q => q.Book)
            .HasForeignKey(q => q.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Annotations)
            .WithOne(a => a.Book)
            .HasForeignKey(a => a.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore computed properties
        builder.Ignore(b => b.ProgressPercentage);
    }
}
```

#### BookGenreConfiguration.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

public class BookGenreConfiguration : IEntityTypeConfiguration<BookGenre>
{
    public void Configure(EntityTypeBuilder<BookGenre> builder)
    {
        // Composite Primary Key
        builder.HasKey(bg => new { bg.BookId, bg.GenreId });

        // Relationships
        builder.HasOne(bg => bg.Book)
            .WithMany(b => b.BookGenres)
            .HasForeignKey(bg => bg.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bg => bg.Genre)
            .WithMany(g => g.BookGenres)
            .HasForeignKey(bg => bg.GenreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(bg => bg.BookId);
        builder.HasIndex(bg => bg.GenreId);
    }
}
```

#### GenreConfiguration.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data.Configurations;

public class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(g => g.Name)
            .IsUnique();
    }
}
```

**Weitere Configurations:** Erstelle analog für alle anderen Entitäten
- `ReadingSessionConfiguration.cs`
- `QuoteConfiguration.cs`
- `AnnotationConfiguration.cs`
- `ReadingGoalConfiguration.cs`
- `PlantSpeciesConfiguration.cs`
- `UserPlantConfiguration.cs`
- `ShopItemConfiguration.cs`
- `AppSettingsConfiguration.cs`

**Muster:** Immer PK, Indexes auf FKs, Required Fields, MaxLength, Relationships, Ignore Computed Properties

### Acceptance Criteria

- [ ] AppDbContext erstellt mit allen DbSets
- [ ] Fluent API Configurations für alle 12 Entitäten
- [ ] Seed Data für Genres, PlantSpecies, AppSettings
- [ ] Indexes auf häufig abgefragte Felder (ISBN, Title, Status, BookId, GenreId)

---

## Arbeitspaket 4: EF Core Migrations

**Aufwand:** 8 Stunden
**Priorität:** P0 (Blocker)

### 4.1 EF Core Tools installieren (global)

```bash
dotnet tool install --global dotnet-ef
# oder update
dotnet tool update --global dotnet-ef
```

### 4.2 Initial Migration erstellen

```bash
# Im Solution-Root
dotnet ef migrations add InitialCreate \
    --project BookLoggerApp.Infrastructure \
    --startup-project BookLoggerApp \
    --output-dir Data/Migrations
```

**Generiert:**
- `YYYYMMDDHHMMSS_InitialCreate.cs` (Up/Down)
- `AppDbContextModelSnapshot.cs`

### 4.3 Migration anwenden

**Entwicklung:**
```bash
dotnet ef database update \
    --project BookLoggerApp.Infrastructure \
    --startup-project BookLoggerApp
```

**Production (App-Start):**

In `MauiProgram.cs`:

```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // DbContext registrieren
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "booklogger.db");

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Services (später in M3)
        // ...

        return builder.Build();
    }
}
```

In `App.xaml.cs`:

```csharp
protected override async void OnStart()
{
    base.OnStart();

    // Auto-migrate database on app start
    using var scope = Handler.MauiContext.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}
```

### 4.4 Datenmigration von alter DB (optional)

Falls User bereits M1-Daten haben:

**Script:** `BookLoggerApp.Infrastructure/Data/LegacyDataMigration.cs`

```csharp
using SQLite;
using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Data;

public static class LegacyDataMigration
{
    public static async Task MigrateFromLegacyDbAsync(string legacyDbPath, AppDbContext newDbContext)
    {
        if (!File.Exists(legacyDbPath))
            return; // No legacy DB

        // Open old SQLite DB (sqlite-net-pcl)
        var legacyDb = new SQLiteAsyncConnection(legacyDbPath);

        // Read old Books
        var oldBooks = await legacyDb.Table<LegacyBook>().ToListAsync();
        foreach (var oldBook in oldBooks)
        {
            var newBook = new Book
            {
                Id = oldBook.Id,
                Title = oldBook.Title,
                Author = oldBook.Author,
                Status = oldBook.Status,
                DateAdded = DateTime.UtcNow // Legacy didn't have this
            };
            newDbContext.Books.Add(newBook);
        }

        // Read old ReadingSessions
        var oldSessions = await legacyDb.Table<LegacySession>().ToListAsync();
        foreach (var oldSession in oldSessions)
        {
            var newSession = new ReadingSession
            {
                Id = oldSession.Id,
                BookId = oldSession.BookId,
                StartedAt = oldSession.StartedAt,
                Minutes = oldSession.Minutes,
                PagesRead = oldSession.PagesRead,
                XpEarned = oldSession.Minutes // Simple XP calc
            };
            newDbContext.ReadingSessions.Add(newSession);
        }

        await newDbContext.SaveChangesAsync();

        // Optionally: Rename old DB file
        File.Move(legacyDbPath, legacyDbPath + ".old");
    }

    // Legacy models (for reading old data)
    class LegacyBook
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public ReadingStatus Status { get; set; }
    }

    class LegacySession
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public Guid BookId { get; set; }
        public DateTime StartedAt { get; set; }
        public int Minutes { get; set; }
        public int? PagesRead { get; set; }
    }
}
```

**Aufruf in App.xaml.cs:**

```csharp
protected override async void OnStart()
{
    base.OnStart();

    using var scope = Handler.MauiContext.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Migrate schema
    await dbContext.Database.MigrateAsync();

    // Migrate legacy data (if exists)
    var legacyDbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "booklogger.db3"); // Old filename
    await LegacyDataMigration.MigrateFromLegacyDbAsync(legacyDbPath, dbContext);
}
```

### Acceptance Criteria

- [ ] Initial Migration generiert und erfolgreich
- [ ] Datenbank wird bei App-Start automatisch migriert
- [ ] Legacy-Daten werden migriert (falls vorhanden)
- [ ] Seed-Daten werden eingefügt (Genres, Plants, Settings)

---

## Arbeitspaket 5: Repository Pattern

**Aufwand:** 16 Stunden
**Priorität:** P1 (High)

### 5.1 Generic Repository Interface

**Location:** `BookLoggerApp.Infrastructure/Repositories/IRepository.cs`

```csharp
using System.Linq.Expressions;

namespace BookLoggerApp.Infrastructure.Repositories;

public interface IRepository<T> where T : class
{
    // Query
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    // Command
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
    Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    // Count
    Task<int> CountAsync(CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    // Exists
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}
```

### 5.2 Generic Repository Implementation

**Location:** `BookLoggerApp.Infrastructure/Repositories/Repository.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, ct);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbSet.ToListAsync(ct);
    }

    public virtual async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(ct);
    }

    public virtual async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate, ct);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        await _dbSet.AddRangeAsync(entities, ct);
        await _context.SaveChangesAsync(ct);
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    public virtual async Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public virtual async Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        _dbSet.RemoveRange(entities);
        await _context.SaveChangesAsync(ct);
    }

    public virtual async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await _dbSet.CountAsync(ct);
    }

    public virtual async Task<int> CountAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet.CountAsync(predicate, ct);
    }

    public virtual async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(predicate, ct);
    }
}
```

### 5.3 Specific Repository (Beispiel: BookRepository)

**Location:** `BookLoggerApp.Infrastructure/Repositories/Specific/IBookRepository.cs`

```csharp
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Infrastructure.Repositories.Specific;

public interface IBookRepository : IRepository<Book>
{
    Task<IReadOnlyList<Book>> GetBooksByStatusAsync(ReadingStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<Book>> GetBooksByGenreAsync(Guid genreId, CancellationToken ct = default);
    Task<Book?> GetBookWithDetailsAsync(Guid id, CancellationToken ct = default); // Includes Sessions, Quotes, etc.
}
```

**Implementation:** `BookRepository.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Infrastructure.Repositories.Specific;

public class BookRepository : Repository<Book>, IBookRepository
{
    public BookRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Book>> GetBooksByStatusAsync(
        ReadingStatus status,
        CancellationToken ct = default)
    {
        return await _dbSet
            .Where(b => b.Status == status)
            .OrderByDescending(b => b.DateAdded)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Book>> GetBooksByGenreAsync(
        Guid genreId,
        CancellationToken ct = default)
    {
        return await _dbSet
            .Include(b => b.BookGenres)
            .Where(b => b.BookGenres.Any(bg => bg.GenreId == genreId))
            .ToListAsync(ct);
    }

    public async Task<Book?> GetBookWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(b => b.ReadingSessions)
            .Include(b => b.Quotes)
            .Include(b => b.Annotations)
            .Include(b => b.BookGenres)
                .ThenInclude(bg => bg.Genre)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }
}
```

### 5.4 Repository Registration in DI

**MauiProgram.cs:**

```csharp
// Generic Repository
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Specific Repositories
builder.Services.AddScoped<IBookRepository, BookRepository>();
// Add more specific repositories as needed
```

### Acceptance Criteria

- [ ] Generic Repository interface & implementation erstellt
- [ ] Specific BookRepository mit Custom Queries
- [ ] Repositories in DI registriert
- [ ] Unit Tests für Repository (mit InMemory DB)

---

## Arbeitspaket 6: Testing

**Aufwand:** 16 Stunden
**Priorität:** P1 (High)

### 6.1 Test Setup

**Location:** `BookLoggerApp.Tests/Infrastructure/`

#### DbContextTestHelper.cs

```csharp
using Microsoft.EntityFrameworkCore;
using BookLoggerApp.Infrastructure.Data;

namespace BookLoggerApp.Tests.Infrastructure;

public static class DbContextTestHelper
{
    public static AppDbContext CreateInMemoryDbContext(string dbName = "TestDb")
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
```

### 6.2 Repository Tests

**Location:** `BookLoggerApp.Tests/Infrastructure/Repositories/BookRepositoryTests.cs`

```csharp
using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.Infrastructure;

namespace BookLoggerApp.Tests.Infrastructure.Repositories;

public class BookRepositoryTests
{
    [Fact]
    public async Task AddAsync_ShouldAddBookToDatabase()
    {
        // Arrange
        using var context = DbContextTestHelper.CreateInMemoryDbContext();
        var repository = new BookRepository(context);

        var book = new Book
        {
            Title = "Test Book",
            Author = "Test Author"
        };

        // Act
        var result = await repository.AddAsync(book);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();

        var savedBook = await repository.GetByIdAsync(result.Id);
        savedBook.Should().NotBeNull();
        savedBook!.Title.Should().Be("Test Book");
    }

    [Fact]
    public async Task GetBooksByStatusAsync_ShouldReturnFilteredBooks()
    {
        // Arrange
        using var context = DbContextTestHelper.CreateInMemoryDbContext("BooksByStatus");
        var repository = new BookRepository(context);

        await repository.AddAsync(new Book { Title = "Book 1", Author = "Author", Status = ReadingStatus.Reading });
        await repository.AddAsync(new Book { Title = "Book 2", Author = "Author", Status = ReadingStatus.Completed });
        await repository.AddAsync(new Book { Title = "Book 3", Author = "Author", Status = ReadingStatus.Reading });

        // Act
        var readingBooks = await repository.GetBooksByStatusAsync(ReadingStatus.Reading);

        // Assert
        readingBooks.Should().HaveCount(2);
        readingBooks.Should().OnlyContain(b => b.Status == ReadingStatus.Reading);
    }

    [Fact]
    public async Task GetBookWithDetailsAsync_ShouldIncludeRelatedData()
    {
        // Arrange
        using var context = DbContextTestHelper.CreateInMemoryDbContext("BookWithDetails");
        var repository = new BookRepository(context);

        var book = new Book { Title = "Test Book", Author = "Author" };
        await repository.AddAsync(book);

        var session = new ReadingSession { BookId = book.Id, Minutes = 30 };
        context.ReadingSessions.Add(session);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetBookWithDetailsAsync(book.Id);

        // Assert
        result.Should().NotBeNull();
        result!.ReadingSessions.Should().HaveCount(1);
        result.ReadingSessions.First().Minutes.Should().Be(30);
    }
}
```

**Weitere Tests:** Analog für alle Repositories

### 6.3 DbContext Tests

**Location:** `BookLoggerApp.Tests/Infrastructure/Data/AppDbContextTests.cs`

```csharp
using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Tests.Infrastructure;

namespace BookLoggerApp.Tests.Infrastructure.Data;

public class AppDbContextTests
{
    [Fact]
    public async Task DbContext_ShouldSeedGenres()
    {
        // Arrange & Act
        using var context = DbContextTestHelper.CreateInMemoryDbContext("SeedTest");

        // Assert
        var genres = await context.Genres.ToListAsync();
        genres.Should().NotBeEmpty();
        genres.Should().Contain(g => g.Name == "Fiction");
        genres.Should().Contain(g => g.Name == "Fantasy");
    }

    [Fact]
    public async Task DbContext_ShouldEnforceBookGenreRelationship()
    {
        // Arrange
        using var context = DbContextTestHelper.CreateInMemoryDbContext("Relationships");

        var genre = new Genre { Name = "Test Genre" };
        context.Genres.Add(genre);
        await context.SaveChangesAsync();

        var book = new Book { Title = "Test Book", Author = "Author" };
        context.Books.Add(book);
        await context.SaveChangesAsync();

        var bookGenre = new BookGenre { BookId = book.Id, GenreId = genre.Id };
        context.BookGenres.Add(bookGenre);
        await context.SaveChangesAsync();

        // Act
        var savedBookGenre = await context.BookGenres
            .Include(bg => bg.Book)
            .Include(bg => bg.Genre)
            .FirstOrDefaultAsync(bg => bg.BookId == book.Id);

        // Assert
        savedBookGenre.Should().NotBeNull();
        savedBookGenre!.Book.Title.Should().Be("Test Book");
        savedBookGenre.Genre.Name.Should().Be("Test Genre");
    }
}
```

### 6.4 Model Tests

**Location:** `BookLoggerApp.Tests/Core/Models/BookTests.cs`

```csharp
using FluentAssertions;
using BookLoggerApp.Core.Models;

namespace BookLoggerApp.Tests.Core.Models;

public class BookTests
{
    [Fact]
    public void Book_ProgressPercentage_ShouldCalculateCorrectly()
    {
        // Arrange
        var book = new Book
        {
            Title = "Test",
            Author = "Author",
            PageCount = 200,
            CurrentPage = 50
        };

        // Act
        var progress = book.ProgressPercentage;

        // Assert
        progress.Should().Be(25);
    }

    [Fact]
    public void Book_ProgressPercentage_ShouldReturnZeroWhenPageCountIsNull()
    {
        // Arrange
        var book = new Book
        {
            Title = "Test",
            Author = "Author",
            PageCount = null,
            CurrentPage = 50
        };

        // Act
        var progress = book.ProgressPercentage;

        // Assert
        progress.Should().Be(0);
    }
}
```

**Weitere Tests:** Für UserPlant.NeedsWater, ReadingGoal.IsActive, etc.

### Acceptance Criteria

- [ ] Repository Tests mit InMemory DB
- [ ] DbContext Tests (Relationships, Seed Data)
- [ ] Model Tests (Computed Properties, Validations)
- [ ] Test Coverage ≥ 80% für Infrastructure Layer

---

## Arbeitspaket 7: Integration & Migration

**Aufwand:** 16 Stunden
**Priorität:** P1 (High)

### 7.1 MauiProgram.cs aktualisieren

**Location:** `BookLoggerApp/MauiProgram.cs`

**NEU:**

```csharp
using BookLoggerApp;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using Microsoft.EntityFrameworkCore;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // Database
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "booklogger.db");

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Repositories
        builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        builder.Services.AddScoped<IBookRepository, BookRepository>();

        // Services (werden in M3 implementiert)
        // builder.Services.AddSingleton<IBookService, BookService>();
        // ... weitere Services

        // ViewModels (werden in M4 aktualisiert)
        // builder.Services.AddTransient<BookListViewModel>();
        // ... weitere ViewModels

        return builder.Build();
    }
}
```

### 7.2 App.xaml.cs aktualisieren (DB Migration)

**Location:** `BookLoggerApp/App.xaml.cs`

```csharp
using BookLoggerApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new MainPage();
    }

    protected override async void OnStart()
    {
        base.OnStart();

        // Initialize database
        await InitializeDatabaseAsync();
    }

    private async Task InitializeDatabaseAsync()
    {
        try
        {
            using var scope = Handler?.MauiContext?.Services?.CreateScope();
            if (scope == null) return;

            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Apply migrations
            await dbContext.Database.MigrateAsync();

            // Migrate legacy data (if exists)
            var legacyDbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "booklogger.db3");

            if (File.Exists(legacyDbPath))
            {
                await LegacyDataMigration.MigrateFromLegacyDbAsync(legacyDbPath, dbContext);
            }
        }
        catch (Exception ex)
        {
            // Log error (TODO: Implement logging)
            System.Diagnostics.Debug.WriteLine($"Database initialization failed: {ex.Message}");
        }
    }
}
```

### 7.3 Alte Service-Implementierungen entfernen

**WICHTIG:** Alte Services (SqliteBookService, InMemoryBookService) funktionieren nicht mehr.

**Optionen:**
1. **Löschen** (empfohlen) - Werden in M3 neu implementiert mit EF Core
2. **Behalten** - Als Referenz, aber nicht registrieren in DI

**Entscheidung für M2:** Kommentieren Sie alte Services aus, aber löschen Sie sie nicht. In M3 werden sie neu implementiert.

### 7.4 Smoke Tests

**Location:** `BookLoggerApp.Tests/Integration/SmokeTests.cs`

```csharp
using FluentAssertions;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Tests.Infrastructure;

namespace BookLoggerApp.Tests.Integration;

public class SmokeTests
{
    [Fact]
    public async Task DbContext_ShouldInitializeWithoutErrors()
    {
        // Arrange & Act
        using var context = DbContextTestHelper.CreateInMemoryDbContext();

        // Assert
        context.Should().NotBeNull();
        var books = await context.Books.ToListAsync();
        books.Should().BeEmpty(); // Fresh DB
    }

    [Fact]
    public async Task DbContext_ShouldHaveSeedData()
    {
        // Arrange & Act
        using var context = DbContextTestHelper.CreateInMemoryDbContext("SeedCheck");

        // Assert
        var genres = await context.Genres.ToListAsync();
        genres.Should().NotBeEmpty("Seed data should be present");

        var plantSpecies = await context.PlantSpecies.ToListAsync();
        plantSpecies.Should().NotBeEmpty("Plant species should be seeded");

        var settings = await context.AppSettings.FirstOrDefaultAsync();
        settings.Should().NotBeNull("Default settings should exist");
    }
}
```

### Acceptance Criteria

- [ ] MauiProgram.cs aktualisiert (DbContext registriert)
- [ ] App.xaml.cs führt Migrations beim Start aus
- [ ] Legacy-Daten werden migriert (falls vorhanden)
- [ ] App startet ohne Fehler
- [ ] Smoke Tests erfolgreich

---

## Definition of Done (M2)

Ein M2-Feature gilt als "Done", wenn:

- [x] Alle 7 Arbeitspakete abgeschlossen
- [x] 12 Models erstellt/erweitert mit Navigation Properties
- [x] AppDbContext mit allen DbSets und Configurations
- [x] Initial Migration generiert und getestet
- [x] Repository Pattern implementiert (Generic + Specific)
- [x] Unit Tests geschrieben (≥80% Coverage für Infrastructure)
- [x] Integration Tests (DbContext, Repositories)
- [x] MauiProgram.cs und App.xaml.cs aktualisiert
- [x] App startet und migriert DB automatisch
- [x] Legacy-Daten werden migriert (wenn vorhanden)
- [x] Seed-Daten (Genres, Plants) werden eingefügt
- [x] CI-Pipeline grün (Tests laufen durch)
- [x] Code reviewed (oder Self-Review mit Checkliste)
- [x] Dokumentation aktualisiert (XML-Kommentare, README)
- [x] Keine kritischen Bugs (P0/P1) offen

---

## Risiken & Mitigation (M2)

| Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|--------|-------------------|--------|------------|
| EF Core Migration schlägt fehl | Mittel | Hoch | Detaillierter Migrationsplan, Backup vor Migration, InMemory Tests |
| Legacy-Daten können nicht migriert werden | Mittel | Mittel | User-Export vor Migration, Fallback auf manuelle Re-Entry |
| Performance-Probleme mit EF Core | Niedrig | Mittel | Indexes setzen, Eager Loading optimieren, Benchmarks |
| Komplexität der Configurations überfordert | Mittel | Niedrig | Fluent API Docs lesen, Start mit einfachen Configs |

---

## Nächste Schritte nach M2

Nach erfolgreichem Abschluss von M2:

1. ✅ **M3 starten:** Core Services & Business Logic
   - Services auf Repository Pattern umstellen
   - Business Logic implementieren (XP-Berechnung, Goal-Tracking, etc.)
   - Unit Tests für alle Services

2. 📝 **Dokumentation:**
   - API-Verträge dokumentieren (Interfaces)
   - ER-Diagramm finalisieren

3. 🐛 **Bugfixes:**
   - Alle M2-Bugs fixen vor M3-Start

---

**Ende Phase M2 Plan**
