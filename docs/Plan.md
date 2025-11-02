# Book Logger App – Projektplan (M2 → Release)

**Version:** 1.0
**Stand:** 2025-10-30
**Autor:** Ben Sowieja
**Ziel:** Vollständiger Produktionsrelease v1.0 mit Pflanzenmechanik

---

## 1. Executive Summary

### 1.1 Projektvision
BookLoggerApp ist eine .NET 9 MAUI Blazor Hybrid Android-App (mit optionaler Windows-Unterstützung) zum Verwalten, Tracken und Gamifizieren des persönlichen Leseverhaltens. Die App kombiniert klassische Bücherverwaltung mit einer motivierenden Pflanzenmechanik.

### 1.2 Aktueller Stand (M1 abgeschlossen)
- **Fortschritt:** ~20% des MVP implementiert
- **Implementiert:**
  - Basis-CRUD für Bücher (sqlite-net-pcl)
  - Einfache Lesesessions
  - Grundlegende Blazor UI (Books.razor, BookDetail.razor)
  - ViewModels (BookListViewModel, BookDetailViewModel)
  - CI-Pipeline mit Tests
- **Fehlend:** 7+ Services, 8+ Models, 6+ UI-Pages, komplette Gamification

### 1.3 Release-Ziel v1.0
**Funktionsumfang:**
1. Vollständige Büchereverwaltung (CRUD, Import/Export, ISBN-Lookup)
2. Lesefortschritt & Sessions mit Statistiken
3. Ratings, Zitate & Annotationen
4. **Pflanzenmechanik** (XP/Growth/Notifications/Shop)
5. Umfassende Statistiken (Streaks, Genres, Trends)
6. Settings (Backups, Theme, Sprache, Privacy)
7. **2D Grid Bookshelf** als zentrale UI (2.5D/3D Toggle → v2.0 Roadmap)

**Zeitrahmen:** 10-14 Wochen (70-98 Arbeitstage) ab jetzt
**Puffer:** 20% eingerechnet
**Team:** 1 Vollzeit-Entwickler

---

## 2. Technische Rahmenbedingungen

### 2.1 Tech Stack

#### Core Technologies
| Komponente | Technologie | Version | Begründung |
|------------|-------------|---------|------------|
| Framework | .NET MAUI Blazor Hybrid | 9.0 | Cross-Platform, moderne UI |
| UI-Layer | Blazor WebView | 9.0 | Component-based, C# statt JS |
| Database | SQLite mit **EF Core** | 9.0 | Migration von sqlite-net-pcl für bessere Migrations |
| MVVM | CommunityToolkit.Mvvm | 8.4.0 | Bewährte Source Generators |
| Testing | xUnit + FluentAssertions | 2.9 / 8.6 | Bestehender Stack |
| CI/CD | GitHub Actions | - | Kostenlos, integriert |

#### Wichtige Entscheidung: Migration zu EF Core
**Status:** Geplant in M2 (Phase 1)
**Grund:**
- Automatische Code-First Migrations
- Bessere LINQ-Unterstützung
- Professionelles Change Tracking
- Einfachere komplexe Queries

**Migration Strategy:**
1. EF Core SQLite NuGet Packages hinzufügen
2. DbContext erstellen mit bestehenden Models
3. Migration generieren (Initial Create)
4. Bestehende DB-Daten migrieren (wenn nötig)
5. Services auf DbContext umstellen
6. sqlite-net-pcl entfernen

### 2.2 Architektur

#### 2.2.1 Solution-Struktur (Ziel)

```
BookLoggerApp.sln
│
├── src/
│   ├── BookLoggerApp/                    # MAUI Hauptprojekt
│   │   ├── Components/
│   │   │   ├── Layout/
│   │   │   │   ├── MainLayout.razor
│   │   │   │   └── NavMenu.razor
│   │   │   ├── Pages/
│   │   │   │   ├── Dashboard.razor       [NEU]
│   │   │   │   ├── Bookshelf.razor       [NEU]
│   │   │   │   ├── Books.razor           [ERWEITERT]
│   │   │   │   ├── BookDetail.razor      [ERWEITERT]
│   │   │   │   ├── ReadingView.razor     [NEU]
│   │   │   │   ├── Goals.razor           [NEU]
│   │   │   │   ├── PlantShop.razor       [NEU]
│   │   │   │   ├── Stats.razor           [NEU]
│   │   │   │   └── Settings.razor        [NEU]
│   │   │   └── Shared/
│   │   │       ├── BookCard.razor        [NEU]
│   │   │       ├── PlantWidget.razor     [NEU]
│   │   │       └── StatCard.razor        [NEU]
│   │   ├── Platforms/                    [Android, iOS, Windows, MacCatalyst]
│   │   ├── Resources/
│   │   ├── wwwroot/
│   │   │   ├── css/
│   │   │   │   ├── app.css
│   │   │   │   └── bookshelf.css         [NEU]
│   │   │   ├── js/
│   │   │   │   └── interop.js            [NEU - für Animationen]
│   │   │   └── images/
│   │   │       ├── plants/               [NEU]
│   │   │       └── placeholders/         [NEU]
│   │   ├── MauiProgram.cs
│   │   └── App.xaml.cs
│   │
│   ├── BookLoggerApp.Core/               # Domain Layer
│   │   ├── Models/
│   │   │   ├── Book.cs                   [ERWEITERT]
│   │   │   ├── Genre.cs                  [NEU]
│   │   │   ├── BookGenre.cs              [NEU]
│   │   │   ├── ReadingSession.cs         [ERWEITERT]
│   │   │   ├── Rating.cs                 [NEU]
│   │   │   ├── Quote.cs                  [NEU]
│   │   │   ├── Annotation.cs             [NEU]
│   │   │   ├── ReadingGoal.cs            [NEU]
│   │   │   ├── Plant.cs                  [NEU]
│   │   │   ├── PlantSpecies.cs           [NEU]
│   │   │   ├── UserPlant.cs              [NEU]
│   │   │   ├── ShopItem.cs               [NEU]
│   │   │   ├── UserStats.cs              [NEU]
│   │   │   └── AppSettings.cs            [NEU]
│   │   ├── Services/
│   │   │   ├── Abstractions/
│   │   │   │   ├── IBookService.cs       [VORHANDEN]
│   │   │   │   ├── IProgressService.cs   [VORHANDEN]
│   │   │   │   ├── IGenreService.cs      [NEU]
│   │   │   │   ├── IRatingService.cs     [NEU]
│   │   │   │   ├── IQuoteService.cs      [NEU]
│   │   │   │   ├── IAnnotationService.cs [NEU]
│   │   │   │   ├── IGoalService.cs       [NEU]
│   │   │   │   ├── IPlantService.cs      [NEU]
│   │   │   │   ├── IStatsService.cs      [NEU]
│   │   │   │   ├── IImportExportService.cs [NEU]
│   │   │   │   ├── ILookupService.cs     [NEU]
│   │   │   │   ├── INotificationService.cs [NEU]
│   │   │   │   └── IImageService.cs      [NEU]
│   │   │   └── [Implementations siehe Phase-Pläne]
│   │   ├── ViewModels/
│   │   │   ├── BookListViewModel.cs      [VORHANDEN]
│   │   │   ├── BookDetailViewModel.cs    [VORHANDEN]
│   │   │   ├── DashboardViewModel.cs     [NEU]
│   │   │   ├── BookshelfViewModel.cs     [NEU]
│   │   │   ├── ReadingViewModel.cs       [NEU]
│   │   │   ├── GoalsViewModel.cs         [NEU]
│   │   │   ├── PlantShopViewModel.cs     [NEU]
│   │   │   ├── StatsViewModel.cs         [NEU]
│   │   │   └── SettingsViewModel.cs      [NEU]
│   │   └── Enums/
│   │       ├── ReadingStatus.cs          [VORHANDEN]
│   │       ├── GoalType.cs               [NEU]
│   │       ├── PlantStatus.cs            [NEU]
│   │       └── ExportFormat.cs           [NEU]
│   │
│   └── BookLoggerApp.Infrastructure/     # Data Access Layer [NEU]
│       ├── Data/
│       │   ├── AppDbContext.cs           [NEU]
│       │   ├── Configurations/           [NEU - EF Configurations]
│       │   └── Migrations/               [NEU - Auto-generiert]
│       ├── Repositories/                 [NEU]
│       │   ├── IRepository.cs
│       │   ├── Repository.cs
│       │   └── [Specific Repositories]
│       └── Services/                     [Service Implementations]
│
├── tests/
│   ├── BookLoggerApp.Tests/              [VORHANDEN]
│   ├── BookLoggerApp.IntegrationTests/   [NEU]
│   └── BookLoggerApp.UITests/            [NEU - Optional]
│
└── docs/
    ├── Plan.md                           [DIESES DOKUMENT]
    ├── Phase_M2_Plan.md
    ├── Phase_M3_Plan.md
    ├── Phase_M4_Plan.md
    ├── Phase_M5_Plan.md
    ├── Phase_M6_Plan.md
    └── Architecture/
        ├── ER_Diagram.md
        └── API_Contracts.md
```

#### 2.2.2 Dependency Injection Diagramm

```
┌─────────────────────────────────────────────────────────────┐
│                     MauiProgram.cs                          │
│                   (DI Configuration)                        │
└────────────────────────┬────────────────────────────────────┘
                         │
        ┌────────────────┼────────────────┐
        │                │                │
        ▼                ▼                ▼
┌──────────────┐  ┌─────────────┐  ┌──────────────┐
│  DbContext   │  │  Services   │  │  ViewModels  │
│  (Singleton) │  │ (Singleton) │  │ (Transient)  │
└──────┬───────┘  └──────┬──────┘  └──────┬───────┘
       │                 │                 │
       │                 │                 │
       ├─────────────────┼─────────────────┤
       │                 │                 │
       ▼                 ▼                 ▼
┌─────────────────────────────────────────────────────┐
│              Blazor Components                       │
│  (Dashboard, Bookshelf, Details, Reading, etc.)     │
└─────────────────────────────────────────────────────┘

Registrierung (Beispiel):

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Repositories
builder.Services.AddScoped<IRepository<Book>, Repository<Book>>();

// Services (Business Logic)
builder.Services.AddSingleton<IBookService, BookService>();
builder.Services.AddSingleton<IPlantService, PlantService>();
builder.Services.AddSingleton<IStatsService, StatsService>();
// ... weitere Services

// ViewModels
builder.Services.AddTransient<DashboardViewModel>();
builder.Services.AddTransient<BookshelfViewModel>();
builder.Services.AddTransient<PlantShopViewModel>();
// ... weitere ViewModels
```

#### 2.2.3 Namespace-Konvention

| Namespace | Zweck | Beispiele |
|-----------|-------|-----------|
| `BookLoggerApp` | UI-Layer (Pages, Components) | `Bookshelf.razor` |
| `BookLoggerApp.Core.Models` | Domain Models | `Book`, `Plant`, `Goal` |
| `BookLoggerApp.Core.ViewModels` | ViewModels (MVVM) | `BookshelfViewModel` |
| `BookLoggerApp.Core.Services.Abstractions` | Service Interfaces | `IBookService` |
| `BookLoggerApp.Core.Enums` | Enumerations | `ReadingStatus` |
| `BookLoggerApp.Infrastructure.Data` | EF Core DbContext | `AppDbContext` |
| `BookLoggerApp.Infrastructure.Repositories` | Data Access | `Repository<T>` |
| `BookLoggerApp.Infrastructure.Services` | Service Implementations | `BookService` |

---

## 3. Datenmodell & Schema

### 3.1 Entity Relationship Diagram (Simplified)

```
┌─────────────┐         ┌─────────────┐         ┌─────────────┐
│    Book     │◄───────┤  BookGenre  ├───────►│    Genre    │
│─────────────│         │─────────────│         │─────────────│
│ Id (PK)     │         │ BookId (FK) │         │ Id (PK)     │
│ Title       │         │ GenreId(FK) │         │ Name        │
│ Author      │         └─────────────┘         │ Description │
│ ISBN        │                                 │ Icon        │
│ PageCount   │                                 └─────────────┘
│ CurrentPage │
│ CoverImage  │
│ Status      │         ┌──────────────┐
│ Rating      │◄────────┤    Rating    │
│ ...         │         │──────────────│
└──────┬──────┘         │ Id (PK)      │
       │                │ BookId (FK)  │
       │                │ Score (1-5)  │
       │                │ ReviewText   │
       │                │ RatedAt      │
       │                └──────────────┘
       │
       │                ┌──────────────────┐
       ├────────────────┤ ReadingSession   │
       │                │──────────────────│
       │                │ Id (PK)          │
       │                │ BookId (FK)      │
       │                │ StartedAt        │
       │                │ Minutes          │
       │                │ PagesRead        │
       │                │ XpEarned         │ [NEU]
       │                └──────────────────┘
       │
       │                ┌──────────────┐
       ├────────────────┤    Quote     │
       │                │──────────────│
       │                │ Id (PK)      │
       │                │ BookId (FK)  │
       │                │ Text         │
       │                │ PageNumber   │
       │                │ CreatedAt    │
       │                └──────────────┘
       │
       │                ┌──────────────┐
       └────────────────┤ Annotation   │
                        │──────────────│
                        │ Id (PK)      │
                        │ BookId (FK)  │
                        │ Note         │
                        │ PageNumber   │
                        │ CreatedAt    │
                        └──────────────┘

┌──────────────┐         ┌──────────────┐         ┌───────────────┐
│ ReadingGoal  │         │  UserPlant   │◄────────┤ PlantSpecies  │
│──────────────│         │──────────────│         │───────────────│
│ Id (PK)      │         │ Id (PK)      │         │ Id (PK)       │
│ Type         │         │ SpeciesId(FK)│         │ Name          │
│ Target       │         │ CurrentLevel │         │ Description   │
│ Current      │         │ Experience   │         │ MaxLevel      │
│ StartDate    │         │ LastWatered  │         │ WaterInterval │
│ EndDate      │         │ Status       │         │ BaseCost      │
│ IsCompleted  │         │ PlantedAt    │         │ ImagePath     │
└──────────────┘         └──────────────┘         │ GrowthRate    │
                                                   └───────────────┘

┌──────────────┐         ┌──────────────┐
│  ShopItem    │         │ AppSettings  │
│──────────────│         │──────────────│
│ Id (PK)      │         │ Id (PK)      │
│ ItemType     │         │ Theme        │
│ Name         │         │ Language     │
│ Description  │         │ Notifications│
│ Cost         │         │ BackupEnabled│
│ ImagePath    │         │ LastBackup   │
│ IsAvailable  │         └──────────────┘
└──────────────┘
```

### 3.2 Kernentitäten (Übersicht)

| Entität | Status | Zweck | Wichtigste Felder |
|---------|--------|-------|-------------------|
| `Book` | Erweitern | Buch-Stammdaten | Title, Author, ISBN, PageCount, CoverImage, Rating |
| `Genre` | Neu | Genres/Kategorien | Name, Description, Icon |
| `BookGenre` | Neu | N:M Relation | BookId, GenreId |
| `ReadingSession` | Erweitern | Lesesessions | BookId, StartedAt, Minutes, PagesRead, XpEarned |
| `Rating` | Neu | Bewertungen | BookId, Score, ReviewText, RatedAt |
| `Quote` | Neu | Zitate | BookId, Text, PageNumber |
| `Annotation` | Neu | Notizen | BookId, Note, PageNumber |
| `ReadingGoal` | Neu | Ziele | Type, Target, Current, StartDate, EndDate |
| `PlantSpecies` | Neu | Pflanzenarten | Name, MaxLevel, WaterInterval, BaseCost |
| `UserPlant` | Neu | User's Pflanzen | SpeciesId, CurrentLevel, Experience, Status |
| `ShopItem` | Neu | Shop-Artikel | ItemType, Name, Cost, IsAvailable |
| `AppSettings` | Neu | Einstellungen | Theme, Language, NotificationsEnabled |

**Detaillierte Schema-Definitionen:** Siehe Phase_M2_Plan.md

### 3.3 EF Core Migration Strategy

**Phase 1 (M2):** Migration von sqlite-net-pcl zu EF Core
- NuGet: `Microsoft.EntityFrameworkCore.Sqlite`
- Initial Migration mit bestehenden Entitäten
- Datenmigration von alter DB (falls vorhanden)

**Laufende Migrations:**
```bash
# Migration erstellen
dotnet ef migrations add MigrationName --project BookLoggerApp.Infrastructure

# Migration anwenden
dotnet ef database update --project BookLoggerApp.Infrastructure

# In Production: Automatische Migration beim App-Start
await dbContext.Database.MigrateAsync();
```

**Schema-Versionierung:**
- Migrations in `Infrastructure/Data/Migrations/`
- Backward-kompatibel (keine Breaking Changes)
- Rollback-Plan für jede Migration

---

## 4. UI/UX Konzept & Wireframes

### 4.1 Navigation Structure

```
┌────────────────────────────────────────────────────────────┐
│                      NavMenu                                │
│  [Dashboard] [Bookshelf] [Reading] [Goals] [Shop] [Stats]  │
└────────────────────────────────────────────────────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        │                   │                   │
        ▼                   ▼                   ▼
   Dashboard            Bookshelf           Reading View
   (Home)               (Main View)         (Active Session)
        │                   │                   │
        │                   ├─► Book Detail     │
        │                   ├─► Add Book        │
        │                   └─► Search/Filter   │
        │                                       │
        ├─► Goals ◄─────────────────────────────┘
        ├─► Plant Shop
        ├─► Stats
        └─► Settings
```

### 4.2 Bookshelf View (Zentrale UI)

**Umsetzung v1.0:** 2D Grid mit Covers
**Roadmap v2.0:** Toggle zu 2.5D/3D Regal-Ansicht

#### ASCII Wireframe: Bookshelf (2D Grid)

```
╔════════════════════════════════════════════════════════════╗
║  BOOKSHELF                    [🔍 Search] [⚙ Filter] [+]   ║
╠════════════════════════════════════════════════════════════╣
║                                                            ║
║  Sort: [Title ▼] [Author] [Status] [Rating] [Added]      ║
║  Filter: [All] [Reading] [Planned] [Completed]            ║
║                                                            ║
╠════════════════════════════════════════════════════════════╣
║  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐ ║
║  │  Cover   │  │  Cover   │  │  Cover   │  │  Cover   │ ║
║  │  Image   │  │  Image   │  │  Image   │  │  Image   │ ║
║  │          │  │          │  │          │  │          │ ║
║  │          │  │          │  │          │  │          │ ║
║  └──────────┘  └──────────┘  └──────────┘  └──────────┘ ║
║  Book Title 1  Book Title 2  Book Title 3  Book Title 4  ║
║  Author Name   Author Name   Author Name   Author Name   ║
║  ★★★★☆ 80%    ★★★☆☆ 45%    ★★★★★ Done    ☆☆☆☆☆ 0%     ║
║                                                            ║
║  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐ ║
║  │  Cover   │  │  Cover   │  │  Cover   │  │  Cover   │ ║
║  │  Image   │  │  Image   │  │  Image   │  │  Image   │ ║
║  │          │  │          │  │          │  │          │ ║
║  │          │  │          │  │          │  │          │ ║
║  └──────────┘  └──────────┘  └──────────┘  └──────────┘ ║
║  Book Title 5  Book Title 6  Book Title 7  Book Title 8  ║
║  Author Name   Author Name   Author Name   Author Name   ║
║  ★★☆☆☆ 10%    ★★★★☆ 60%    ★★★★★ Done    ☆☆☆☆☆ 0%     ║
║                                                            ║
║  [Load More... 24 of 156 books]                           ║
╚════════════════════════════════════════════════════════════╝

Interaktionen:
- Tap auf Cover → BookDetail Page
- Long Press → Kontextmenü (Edit, Delete, Change Status)
- Drag & Drop → Manuelles Sortieren (optional v1.1)
- Swipe → Quick Actions (Mark as Read, Add to Reading)
```

**Technische Umsetzung:**
- Blazor Component: `Bookshelf.razor`
- ViewModel: `BookshelfViewModel`
- Virtualisierung mit `Virtualize<T>` für Performance
- Lazy Loading von Cover Images
- Grid Layout mit CSS Grid / Flexbox
- Responsive: 2-4 Spalten je nach Bildschirmgröße

**Performance-Ziele:**
- Initial Render: < 500ms für 20 Bücher
- Scroll Performance: 60 FPS
- Lazy Load Images: max 200ms per Image

### 4.3 Dashboard View

```
╔════════════════════════════════════════════════════════════╗
║  DASHBOARD                                    [🌱 Plant]   ║
╠════════════════════════════════════════════════════════════╣
║                                                            ║
║  ┌────────────────────────────────────────────────────┐   ║
║  │  Currently Reading                                  │   ║
║  │  ┌─────────┐  "The Great Gatsby"                   │   ║
║  │  │ Cover   │  by F. Scott Fitzgerald                │   ║
║  │  │         │  Page 89 / 180 (49%)                   │   ║
║  │  │         │  [Continue Reading →]                  │   ║
║  │  └─────────┘                                        │   ║
║  └────────────────────────────────────────────────────┘   ║
║                                                            ║
║  ┌──────────────────────┐  ┌──────────────────────────┐  ║
║  │  📊 This Week         │  │  🎯 Active Goals         │  ║
║  │  ─────────────────    │  │  ─────────────────       │  ║
║  │  Books Read: 2        │  │  📖 Read 5 books/month  │  ║
║  │  Time: 6h 32m         │  │  ████░░░░░ 80% (4/5)    │  ║
║  │  Pages: 487           │  │                          │  ║
║  │  XP Earned: 1,240     │  │  ⏱ Read 10h/week       │  ║
║  │                       │  │  ███████░░ 65% (6.5h)   │  ║
║  └──────────────────────┘  └──────────────────────────┘  ║
║                                                            ║
║  ┌────────────────────────────────────────────────────┐   ║
║  │  🌱 Your Plant: "Bookworm Fern"                    │   ║
║  │  ┌──────────┐  Level 7 | XP: 2,340 / 3,000        │   ║
║  │  │          │  ████████░░░░░░ 78%                  │   ║
║  │  │  Plant   │  Status: Healthy 😊                  │   ║
║  │  │  Image   │  Last watered: 2 days ago            │   ║
║  │  │          │  [Water Plant] [Visit Shop →]        │   ║
║  │  └──────────┘                                       │   ║
║  └────────────────────────────────────────────────────┘   ║
║                                                            ║
║  Recent Activity                                          ║
║  • Finished "1984" by George Orwell        ★★★★★         ║
║  • Added quote from "The Great Gatsby"     2 hours ago   ║
║  • Unlocked achievement: "Week Warrior"    1 day ago     ║
║                                                            ║
╚════════════════════════════════════════════════════════════╝
```

### 4.4 Book Detail View

```
╔════════════════════════════════════════════════════════════╗
║  [← Back]  BOOK DETAIL                [Edit] [Delete]     ║
╠════════════════════════════════════════════════════════════╣
║  ┌─────────────┐                                          ║
║  │             │  The Great Gatsby                        ║
║  │   Cover     │  by F. Scott Fitzgerald                  ║
║  │   Image     │  ─────────────────────────────           ║
║  │             │  ★★★★☆ 4.5/5                             ║
║  │             │  Genre: Classic, Fiction                 ║
║  └─────────────┘  ISBN: 978-0-7432-7356-5                ║
║                   Pages: 180 | Current: 89 (49%)          ║
║                   Status: [Reading ▼]                     ║
║                                                            ║
║  ┌────────────────────────────────────────────────────┐   ║
║  │  Progress                                          │   ║
║  │  ████████████████████░░░░░░░░░░░░░░░░░░ 49%       │   ║
║  │  Started: Jan 15, 2025 | Est. Finish: Feb 3, 2025 │   ║
║  └────────────────────────────────────────────────────┘   ║
║                                                            ║
║  [Start Reading Session] [Add Note] [Add Quote]           ║
║                                                            ║
║  ─── Description ─────────────────────────────────────    ║
║  The Great Gatsby is a 1925 novel by American writer...   ║
║                                                            ║
║  ─── Reading Sessions (8 total, 6h 32m) ─────────────    ║
║  • Jan 28, 2025 - 45 min, 23 pages (+45 XP)              ║
║  • Jan 26, 2025 - 1h 12m, 38 pages (+72 XP)              ║
║  • Jan 24, 2025 - 32 min, 15 pages (+32 XP)              ║
║  [View All Sessions →]                                    ║
║                                                            ║
║  ─── Quotes (3) ──────────────────────────────────────    ║
║  💬 "So we beat on, boats against the current..."         ║
║     Page 180 | Added Jan 28, 2025                        ║
║  [View All Quotes →]                                      ║
║                                                            ║
║  ─── Notes (2) ───────────────────────────────────────    ║
║  📝 "Interesting symbolism with the green light..."       ║
║     Page 21 | Added Jan 16, 2025                         ║
║  [View All Notes →]                                       ║
║                                                            ║
╚════════════════════════════════════════════════════════════╝
```

**Weitere Wireframes:** Siehe jeweilige Phase-Pläne
- Reading View (M4)
- Goals View (M5)
- Plant Shop (M5)
- Stats Dashboard (M6)
- Settings (M6)

---

## 5. Meilensteine & Timeline

### 5.1 Übersicht

| Meilenstein | Dauer | Start | Ende | Ziel | Deliverables |
|-------------|-------|-------|------|------|--------------|
| **M2** | 2.0 Wo | KW 45 | KW 46 | EF Core Migration & Datenmodell | DbContext, Migrations, 12 Models |
| **M3** | 2.5 Wo | KW 47 | KW 49 | Core Services & Business Logic | 10+ Services, Unit Tests |
| **M4** | 2.0 Wo | KW 50 | KW 51 | UI Implementation | 7 Pages, Bookshelf, ViewModels |
| **M5** | 2.0 Wo | KW 52 | KW 01 | Pflanzenmechanik & Gamification | Plant System, Shop, Notifications |
| **M6** | 1.5 Wo | KW 02 | KW 03 | Polish, Testing & Performance | CI/CD, E2E Tests, Optimierungen |
| **Release** | - | - | **KW 04** | **v1.0 GA** | Play Store / Windows Store |

**Gesamtdauer:** 10 Wochen (70 Arbeitstage)
**Puffer:** +2 Wochen (20%)
**Worst-Case:** 12 Wochen (84 Arbeitstage)

### 5.2 Kritischer Pfad

```
M2: DB Migration ──► M3: Services ──► M4: UI ──► M5: Plants ──► M6: Polish ──► Release
       │                  │              │            │              │
       │                  │              │            │              └─► CI/CD Setup
       │                  │              │            └────────────────► Notifications
       │                  │              └─────────────────────────────► Bookshelf UI
       │                  └────────────────────────────────────────────► Stats Service
       └───────────────────────────────────────────────────────────────► EF Core Setup

Abhängigkeiten:
- M3 blockiert durch M2 (DbContext muss existieren)
- M4 blockiert durch M3 (Services werden von ViewModels genutzt)
- M5 parallel zu M4 möglich (verschiedene Features)
- M6 kann parallel starten (Tests, CI/CD unabhängig)
```

### 5.3 Ressourcenplanung

**Annahme:** 1 Vollzeit-Entwickler, 8h/Tag, 5 Tage/Woche

| Phase | Best Case | Expected | Worst Case | Puffer |
|-------|-----------|----------|------------|--------|
| M2 | 64h (8d) | 80h (10d) | 96h (12d) | 20% |
| M3 | 80h (10d) | 100h (12.5d) | 120h (15d) | 20% |
| M4 | 64h (8d) | 80h (10d) | 96h (12d) | 20% |
| M5 | 64h (8d) | 80h (10d) | 96h (12d) | 20% |
| M6 | 48h (6d) | 60h (7.5d) | 72h (9d) | 20% |
| **Total** | **320h** | **400h** | **480h** | **20%** |
| **Tage** | **40d** | **50d** | **60d** | **+20%** |
| **Wochen** | **8 Wo** | **10 Wo** | **12 Wo** | **+20%** |

### 5.4 Risiken & Mitigation

| Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|--------|-------------------|--------|------------|
| EF Core Migration komplex | Mittel | Hoch | Detaillierter Migrationsplan, Backup-Strategie |
| Plant-Mechanik zu komplex | Hoch | Mittel | Simplifizierten MVP-Ansatz wählen |
| Performance-Probleme | Mittel | Hoch | Frühzeitige Performance-Tests, Profiling |
| UI/UX nicht intuitiv | Mittel | Mittel | Frühe User-Tests, Iteratives Design |
| Scope Creep | Hoch | Hoch | Striktes Backlog-Management, DoD |
| Testing-Aufwand unterschätzt | Mittel | Mittel | Test-First-Approach, automatisierte Tests |

---

## 6. Qualitätssicherung

### 6.1 Test-Strategie

| Test-Ebene | Framework | Coverage-Ziel | Verantwortlich |
|------------|-----------|---------------|----------------|
| **Unit Tests** | xUnit + FluentAssertions | 80% (Services, ViewModels) | Jeder Entwickler |
| **Integration Tests** | xUnit + InMemory EF | 60% (Service-DB-Interaktion) | M3, M5 |
| **UI Tests** | Playwright / bUnit (optional) | 40% (kritische Flows) | M6 |
| **Performance Tests** | BenchmarkDotNet | 100% (kritische Operationen) | M6 |
| **Manual Testing** | Testplan-Checkliste | 100% (alle User Stories) | M6 |

### 6.2 Performance-Budget

| Metrik | Target | Maximum | Messung |
|--------|--------|---------|---------|
| Cold Start (Android) | < 1.5s | < 2.0s | App Launch bis UI ready |
| Bookshelf Render (20 items) | < 300ms | < 500ms | ComponentDidMount bis Paint |
| Navigation Latency | < 80ms | < 120ms | Click bis Page Transition |
| DB Query (Simple) | < 5ms | < 10ms | P50 Latenz |
| DB Query (Complex) | < 15ms | < 30ms | P50 Latenz (Joins, Aggregates) |
| Image Load | < 150ms | < 300ms | Request bis Display |
| Memory Footprint | < 100MB | < 150MB | Idle State |
| APK Size | < 25MB | < 35MB | Release Build |

### 6.3 Definition of Done (DoD)

Ein Feature gilt als "Done", wenn:

- [ ] Code geschrieben und reviewed
- [ ] Unit Tests geschrieben (min. 80% Coverage für neue Services)
- [ ] Integration Tests für DB-Operationen (falls zutreffend)
- [ ] UI manuell getestet auf Android & Windows
- [ ] Performance-Ziele erreicht (siehe Budget)
- [ ] Dokumentation aktualisiert (XML-Kommentare, README)
- [ ] CI-Pipeline grün (Build + Tests)
- [ ] Code-Review durch zweite Person (oder Self-Review mit Checkliste)
- [ ] Keine kritischen Bugs (Blocker) offen
- [ ] Acceptance Criteria der User Story erfüllt

---

## 7. CI/CD & Deployment

### 7.1 GitHub Actions Workflow (Ziel-Zustand)

**Bestehend (M1):**
- Build Core & Tests auf Ubuntu
- Unit Tests mit xUnit
- Test Results Publishing

**Erweiterungen (M6):**
- Android APK Build (Release)
- Windows MSIX Build (Release, optional)
- Code Signing
- Automated Releases (GitHub Releases)
- Store Deployment (Play Store via fastlane, optional)

**Workflow-Übersicht:**

```yaml
name: CI/CD

on:
  push:
    branches: [ main, dev ]
  pull_request:
    branches: [ main ]
  release:
    types: [ published ]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - Checkout
      - Setup .NET 9
      - Restore packages
      - Build Core & Tests
      - Run Unit Tests
      - Run Integration Tests (EF InMemory)
      - Publish Test Results
      - Upload Coverage Report

  build-android:
    needs: test
    runs-on: windows-latest
    if: github.ref == 'refs/heads/main' || github.event_name == 'release'
    steps:
      - Checkout
      - Setup .NET 9
      - Setup Android SDK
      - Restore & Build Android Project
      - Sign APK (Release Keystore from Secrets)
      - Upload APK Artifact
      - (Optional) Deploy to Play Store Internal Track

  build-windows:
    needs: test
    runs-on: windows-latest
    if: github.ref == 'refs/heads/main' || github.event_name == 'release'
    steps:
      - Checkout
      - Setup .NET 9
      - Restore & Build Windows Project
      - Package MSIX
      - Sign MSIX (Certificate from Secrets)
      - Upload MSIX Artifact

  release:
    needs: [build-android, build-windows]
    runs-on: ubuntu-latest
    if: github.event_name == 'release'
    steps:
      - Download APK Artifact
      - Download MSIX Artifact
      - Upload to GitHub Release
```

**Detaillierter Workflow:** Siehe Phase_M6_Plan.md

### 7.2 Release-Strategie

**Versionierung:** Semantic Versioning (MAJOR.MINOR.PATCH)
- v1.0.0 = MVP Release
- v1.1.0 = Minor Features (z.B. 2.5D Bookshelf Toggle)
- v1.0.1 = Bugfix

**Release-Kanäle:**

| Kanal | Zweck | Frequenz | Audience |
|-------|-------|----------|----------|
| **Internal** | Dev Builds | Täglich (dev branch) | Entwickler |
| **Alpha** | Feature Testing | Wöchentlich | Internal Testers |
| **Beta** | User Testing | Alle 2 Wochen | Beta-Tester (50-100 User) |
| **RC** | Release Candidate | 1 Woche vor GA | Öffentlich (opt-in) |
| **GA** | Production | Nach RC (wenn stable) | Alle User (Play Store) |

**Rollout-Plan:**
1. **Woche 11 (nach M6):** Alpha Release → Internal Testing
2. **Woche 12:** Beta Release → 50 Tester, Feedback-Runde
3. **Woche 13:** RC → Bugfixes, Performance-Tuning
4. **Woche 14:** **GA v1.0** → Play Store (staged rollout: 10% → 50% → 100%)

---

## 8. Security & Privacy

### 8.1 Datenschutz-Prinzipien

- **Local-First:** Alle Daten lokal auf dem Gerät (SQLite)
- **Keine Third-Party Tracker:** Keine Analytics-SDKs (Google Analytics, etc.)
- **Keine Accounts:** Keine User-Authentifizierung erforderlich
- **Export-Control:** User kann Daten jederzeit exportieren (CSV, JSON)
- **Lösch-Option:** User kann Daten komplett löschen (Settings → Delete All Data)

### 8.2 Berechtigungen (Android)

| Permission | Erforderlich | Zweck |
|------------|--------------|-------|
| `READ_EXTERNAL_STORAGE` | Optional | Cover-Image Upload, Backup Import |
| `WRITE_EXTERNAL_STORAGE` | Optional | Backup Export |
| `INTERNET` | Nein | Nur für ISBN-Lookup (opt-in Feature) |
| `POST_NOTIFICATIONS` | Optional | Reading Reminders |
| `CAMERA` | Optional | Barcode-Scanner (Future Feature) |

### 8.3 Sicherheits-Maßnahmen

- **SQL Injection:** Verhindert durch EF Core Parameterized Queries
- **Input Validation:** Alle User-Inputs validiert (Länge, Format)
- **Secure Defaults:** Notifications standardmäßig deaktiviert
- **Transparent Logging:** Keine versteckten Daten-Transfers

---

## 9. Telemetrie & Analytics (Self-Hosted)

### 9.1 Konzept

**Problem:** Keine Third-Party Analytics, aber Entwickler braucht Insights (Crashes, Performance)

**Lösung:** Opt-In Self-Hosted Telemetrie
- User muss explizit zustimmen (Settings)
- Daten an eigenen Server (kein Google/Microsoft)
- Open-Source Lösung (z.B. Plausible, Umami, oder custom)

**Gesammelte Daten (anonymisiert):**
- Crashes & Exceptions (Stacktraces ohne PII)
- Performance-Metriken (Cold Start, Navigation Latency)
- Feature-Usage (welche Features werden genutzt?)
- Gerät-Info (OS-Version, Screen Size, RAM)

**NICHT gesammelt:**
- Buch-Daten, Zitate, Notizen
- User-IDs, E-Mails, Namen
- Genaue GPS-Location

### 9.2 Implementation

- **M6:** Telemetrie-Service implementieren (optional)
- **Settings-UI:** Toggle "Help improve BookLogger by sending anonymous usage data"
- **Backend:** Simple REST API für Telemetrie-Events (optional, kann später kommen)

---

## 10. Dokumentation & Support

### 10.1 Entwickler-Dokumentation

- **CLAUDE.md:** Guidance für Claude Code (bereits vorhanden)
- **Plan.md:** Dieses Dokument
- **Phase_Mx_Plan.md:** Detaillierte Meilenstein-Pläne
- **README.md:** User-facing Projekt-Übersicht
- **API_Contracts.md:** Service-Interfaces & Datenmodelle (M3)
- **Architecture/:** Diagramme, Entscheidungen (ADRs)

### 10.2 User-Dokumentation

- **In-App Help:** Tooltips, Onboarding-Flow (M6)
- **GitHub Wiki:** FAQs, Troubleshooting (nach GA)
- **Release Notes:** Changelogs für jede Version

---

## 11. Post-Release Roadmap (v1.1+)

### v1.1 (Q2 2025)
- 2.5D/3D Bookshelf Toggle
- Dark Mode (Auto/Manual)
- Barcode-Scanner für ISBN-Lookup
- Goodreads Import

### v1.2 (Q3 2025)
- Cloud-Sync (Pro-Feature)
- Multi-Device Support
- Advanced Statistics (Charts, Trends)
- Social Features (Share Quotes)

### v2.0 (Q4 2025)
- Reading Challenges (Community)
- AI-Powered Book Recommendations
- Audiobook Integration
- iOS Release

---

## 12. Anhang

### 12.1 Glossar

| Begriff | Definition |
|---------|------------|
| **XP** | Experience Points - Punkte für Lese-Aktivitäten |
| **Plant Species** | Pflanzenarten im Shop (Farn, Kaktus, etc.) |
| **User Plant** | Instanz einer Pflanze im Besitz des Users |
| **Reading Session** | Einzelne Lesesitzung mit Start, Dauer, Seiten |
| **Reading Goal** | Leseziel (z.B. 5 Bücher/Monat, 10h/Woche) |
| **Streak** | Anzahl aufeinanderfolgender Tage mit Leseaktivität |

### 12.2 Referenzen

- [.NET MAUI Docs](https://learn.microsoft.com/dotnet/maui)
- [Blazor Hybrid](https://learn.microsoft.com/aspnet/core/blazor/hybrid)
- [EF Core SQLite](https://learn.microsoft.com/ef/core/providers/sqlite)
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm)

### 12.3 Kontakt

- **Entwickler:** Ben Sowieja
- **GitHub:** https://github.com/TristanAtze/BookLoggerApp
- **Issues:** https://github.com/TristanAtze/BookLoggerApp/issues

---

## 13. Delivery Checklist

Am Ende von M6 (vor Release) muss diese Checkliste vollständig abgehakt sein:

```yaml
delivery_checklist:
  requirements:
    - id: REQ-001
      description: "Alle MVP-Features implementiert (Bücher, Sessions, Goals, Plants, Stats)"
      status: pending
      owner: Dev Team
    - id: REQ-002
      description: "UI/UX entspricht Wireframes (Dashboard, Bookshelf 2D Grid, Details, Reading, Shop, Stats, Settings)"
      status: pending
      owner: Dev Team
    - id: REQ-003
      description: "Performance-Budget eingehalten (Cold Start <1.8s, Navigation <120ms, DB <10ms)"
      status: pending
      owner: Dev Team

  technical:
    - id: TECH-001
      description: "EF Core Migration abgeschlossen, alle Entitäten migriert"
      status: pending
      owner: Dev Team
    - id: TECH-002
      description: "Alle Services implementiert und getestet (10+ Services)"
      status: pending
      owner: Dev Team
    - id: TECH-003
      description: "Unit Test Coverage ≥ 80% für Core & Services"
      status: pending
      owner: Dev Team
    - id: TECH-004
      description: "Integration Tests für kritische DB-Operationen"
      status: pending
      owner: Dev Team
    - id: TECH-005
      description: "CI/CD Pipeline funktioniert (Android APK Build, Windows optional)"
      status: pending
      owner: DevOps

  quality:
    - id: QA-001
      description: "Keine kritischen Bugs (P0/P1) offen"
      status: pending
      owner: QA
    - id: QA-002
      description: "Manual Testing aller User Flows abgeschlossen"
      status: pending
      owner: QA
    - id: QA-003
      description: "Accessibility AA-konform (Kontrast, Dynamic Type, Screen Reader)"
      status: pending
      owner: Dev Team
    - id: QA-004
      description: "Performance-Tests durchgeführt, Bottlenecks behoben"
      status: pending
      owner: Dev Team

  legal:
    - id: LEG-001
      description: "Datenschutzerklärung erstellt (DSGVO-konform)"
      status: pending
      owner: Legal
    - id: LEG-002
      description: "Lizenzen geprüft (Open Source Dependencies)"
      status: pending
      owner: Dev Team
    - id: LEG-003
      description: "Play Store Listing vorbereitet (Beschreibung, Screenshots, Privacy Policy)"
      status: pending
      owner: Marketing

  deployment:
    - id: DEP-001
      description: "Alpha Release erfolgreich (Internal Testing)"
      status: pending
      owner: Release Manager
    - id: DEP-002
      description: "Beta Release erfolgreich (50+ Tester, Feedback eingearbeitet)"
      status: pending
      owner: Release Manager
    - id: DEP-003
      description: "RC Release erfolgreich (keine kritischen Bugs)"
      status: pending
      owner: Release Manager
    - id: DEP-004
      description: "APK signiert mit Release Keystore"
      status: pending
      owner: DevOps
    - id: DEP-005
      description: "Play Store Submission vorbereitet (alle Assets, Beschreibung, Screenshots)"
      status: pending
      owner: Marketing
    - id: DEP-006
      description: "Rollback-Plan dokumentiert (für Post-Release Hotfixes)"
      status: pending
      owner: DevOps

  documentation:
    - id: DOC-001
      description: "README.md aktualisiert (Installation, Features, Screenshots)"
      status: pending
      owner: Dev Team
    - id: DOC-002
      description: "CHANGELOG.md erstellt (v1.0 Release Notes)"
      status: pending
      owner: Dev Team
    - id: DOC-003
      description: "API-Dokumentation vollständig (XML Comments)"
      status: pending
      owner: Dev Team
    - id: DOC-004
      description: "User-Dokumentation (In-App Help, FAQs)"
      status: pending
      owner: Marketing
```

---

**Ende des Hauptdokuments.**

**Nächste Schritte:**
1. Lesen Sie die detaillierten Phasenpläne (Phase_M2_Plan.md bis Phase_M6_Plan.md)
2. Beginnen Sie mit M2: EF Core Migration & Datenmodell
3. Halten Sie sich an die Definition of Done und Performance-Budgets
4. Nutzen Sie die Delivery Checklist als finale Abnahme

**Viel Erfolg beim Build! 🚀📚🌱**
