# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BookLoggerApp (branded as **BookHeart**) is a .NET 10 MAUI Blazor Hybrid Android app for managing and tracking books. It uses Entity Framework Core with SQLite for local data storage and follows a layered architecture with Repository and Unit of Work patterns. It includes gamification features (XP/levels, plant growing, decoration shop, shelf decorations) — see `XP_CALCULATION_GUIDE.md` for the full progression system.

**Companion docs** (read when relevant):
- `AGENTS.md` — repo guidelines with DI, testing, commit/PR conventions (overlaps this file but shorter)
- `XP_CALCULATION_GUIDE.md` — XP, levels, and progression math
- `SECURITY.md` — security posture (read before touching import/export, image handling, URL parsing)
- `CHANGELOG.md` — user-facing change log (German, see section below)

## Build and Test Commands

```bash
# Build entire solution
dotnet build BookLoggerApp.sln

# Run all tests
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj

# Run specific test by name
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~YourTestName"

# Run tests with trx output (CI-style)
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj -c Release --logger "trx;LogFileName=test_results.trx"
```

### EF Core Migrations

```bash
# Add a new migration (run from solution root)
dotnet ef migrations add MigrationName --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp

# Update database
dotnet ef database update --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp

# List migrations
dotnet ef migrations list --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp
```

## Code Style

- `.editorconfig` enforces: UTF-8, LF line endings, 4-space indent, trim trailing whitespace, final newline required
- C#: braces on new line (`csharp_new_line_before_open_brace = all`), `var` for apparent/built-in types only (`var_elsewhere = false`)
- System directives first, separate import groups, no unnecessary `this.` qualification
- Nullable reference types enabled
- Async methods should accept `CancellationToken` parameters
- ViewModels use `[ObservableProperty]` attribute (CommunityToolkit.Mvvm source generators)
- Testing: xUnit + FluentAssertions, arrange-act-assert pattern

## Architecture

### Project Structure

The solution follows a layered architecture with four main projects:

1. **BookLoggerApp.Core** - Domain layer (net10.0)
   - Domain models and entities in `Models/`
   - Enums in `Enums/` (`ExportFormat`, `GoalType`, `PlantStatus`, `ShopItemType`, `WishlistPriority`)
   - Service interfaces in `Services/Abstractions/`
   - ViewModels using CommunityToolkit.Mvvm in `ViewModels/`
   - Helper classes: `XpCalculator`, `SpineColorHelper` in `Helpers/`
   - `DatabaseInitializationHelper` in `Infrastructure/` — provides `EnsureInitializedAsync()` gate
   - Custom exceptions in `Exceptions/`
   - FluentValidation validators in `Validators/`

2. **BookLoggerApp.Infrastructure** - Infrastructure layer (net10.0)
   - Entity Framework Core `AppDbContext` and entity configurations in `Data/Configurations/`
   - `AppDbContextFactory` (design-time `IDesignTimeDbContextFactory`) in `Data/`
   - Seed data: `PlantSeedData`, `TropeSeedData` in `Data/SeedData/`
   - Unit of Work pattern implementation (individual repositories are NOT registered in DI)
   - Service implementations in `Services/`
   - Helper: `PlantGrowthCalculator` in `Services/Helpers/`
   - Database initialization via `DbInitializer`

3. **BookLoggerApp** - Presentation layer (net10.0-android primarily)
   - Blazor components and pages in `Components/`
   - Entry point: `MauiProgram.cs` for DI configuration
   - MAUI-specific services in `Services/` (permissions, scanner, file picker, migration, timer state)
   - Platform-specific: `CustomWebChromeClient` (Android) for camera/WebView permissions
   - `DatabaseMigrationHelper` — handles legacy database path migration (from `Personal` to `LocalApplicationData`)
   - JavaScript interop files in `wwwroot/js/` (see JS Interop section)

4. **BookLoggerApp.Tests** - Test project (net10.0)
   - Uses xUnit, FluentAssertions, NSubstitute (mocking), EF Core InMemory provider
   - Test helpers in `TestHelpers/`: `TestDbContext`, `TestDbContextFactory` (same file), `DbContextTestHelper`
   - Mock services: `MockBookService`, `MockGoalService`, `MockPlantService`, `MockProgressionService`
   - Organized by category: `Unit/` (ViewModels, Validators, Helpers), `Integration/`, `Services/`, `Repositories/`, `Security/` (zip-slip, image security), `Models/`, `Infrastructure/`
   - **Cannot reference the MAUI project** — Tests target `net10.0` while MAUI targets `net10.0-android`. Mock MAUI-layer services via NSubstitute against their interfaces.

### Core Architecture Patterns

**Repository + Unit of Work:**
- Generic `IRepository<T>` for common CRUD operations
- Specific repositories: `IBookRepository`, `IReadingSessionRepository`, `IReadingGoalRepository`, `IUserPlantRepository`
- Only `IUnitOfWork` is registered in DI (transient) — it creates all repositories internally with a shared DbContext
- All services should use `IUnitOfWork`, not direct repository injection

**Entity Framework Core:**
- `AppDbContext` manages entity configurations (in `Data/Configurations/`)
- SQLite provider with EF Core migrations
- Database path: `PlatformsDbPath.GetDatabasePath()` (in Core) → `booklogger.db3` in `LocalApplicationData`
- Both `DbContextFactory<AppDbContext>` and direct `AppDbContext` registered as **transient**

**Dependency Injection (MauiProgram.cs):**
- `DbContext` and `DbContextFactory`: **transient**
- `IUnitOfWork`: **transient**
- Business services (BookService, ProgressService, etc.): **transient** — avoids DbContext tracking issues in Blazor
- `ILookupService`: registered via `AddHttpClient<>` (typed HttpClient, 10s timeout) — not a plain transient
- `AddMemoryCache()` is registered for `IMemoryCache` usage
- Singletons: `IFileSystem`, `IFileSaverService`, `IBackButtonService`, `IAppSettingsProvider`, `IPermissionService`, `IScannerService`, `IShareService`, `IFilePickerService`, `IMigrationService`, `ITimerStateService`
- ViewModels: **transient**
- Validators: **transient**
- Registration methods: `RegisterDatabase()`, `RegisterRepositories()`, `RegisterBusinessServices()`, `RegisterViewModels()`, `RegisterValidators()`
- `DatabaseMigrationHelper.MigrateIfNecessary()` runs synchronously inside `RegisterDatabase()` before DbContext registration

**Service Layer:**
- Service interfaces in `BookLoggerApp.Core/Services/Abstractions/`
- Implementations in `BookLoggerApp.Infrastructure/Services/`
- Core domain services: `IBookService`, `IProgressService`, `IProgressionService`, `IGoalService`, `IPlantService`, `IStatsService`, `IGenreService`, `IQuoteService`, `IAnnotationService`, `IImageService`, `IImportExportService`, `IValidationService`, `INotificationService`, `ILookupService`, `IShelfService`, `IWishlistService`, `IAppSettingsProvider`
- Gamification/content: `IDecorationService` (shelf decorations, separate from plants), `IOnboardingService` (missions, tutorial, feature atlas)
- App lifecycle/meta: `IAppVersionService`, `IAppUpdateService`, `IAppRestartService`, `IChangelogService`, `IReviewPromptService`, `IShareCardService` (PNG generation for book/stats sharing), `IWidgetUpdateService` (Android home-screen widgets)
- MAUI-specific (implementations in `BookLoggerApp/Services/`): `IPermissionService`, `IScannerService`, `IShareService`, `IFilePickerService`, `IMigrationService`, `ITimerStateService`

**Onboarding System:**
- Mission-based first-run experience driven by `IOnboardingService` and catalog models in `Core/Models/` (`OnboardingMissionCatalog`, `OnboardingMissionDefinition`, `OnboardingMissionState`, `OnboardingMissionProgress`, `OnboardingSnapshot`, `OnboardingFeatureAtlasEntry`, `OnboardingIntroStatus`, `OnboardingEvent`)
- UI: `OnboardingTutorial.razor`, `AppStartupOverlay.razor`, `GettingStarted.razor` page, `GettingStartedCta.razor` card
- `AppStartupViewModel` coordinates first-launch flow

**Decoration System:**
- Shelf decorations are distinct from plants — users buy/place non-plant items (frames, lamps, etc.) via `IDecorationService`
- Models: `UserDecoration`, `DecorationShelf`
- ViewModel: `DecorationShopViewModel`
- UI: `DecorationCard.razor`, `DecorationShopCard.razor`

**Google Books API Integration:**
- `LookupService` queries `googleapis.com/books/v1/volumes` for ISBN lookup and book search
- Retry logic: 3 retries at 1s, 3s, 6s delays
- API key configured via `BookLoggerApp.Infrastructure/Services/ApiKeys.cs` (gitignored) — see `ApiKeys.cs.template` for setup. Anonymous access works with rate limits.

**Exception Handling:**
- Custom hierarchy in `BookLoggerApp.Core/Exceptions/`: `BookLoggerException` → `EntityNotFoundException`, `ConcurrencyException`, `InsufficientFundsException`, `ValidationException`
- Global handler in `MauiProgram.cs:SetupGlobalExceptionHandler()` catches `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`

**Validation:**
- FluentValidation validators in `BookLoggerApp.Core/Validators/` for `Book`, `ReadingSession`, `ReadingGoal`, `UserPlant`
- Integrated via `IValidationService`

**Database Initialization:**
- `DbInitializer.InitializeAsync()` runs fire-and-forget in background (Infrastructure)
- ViewModels should call `DatabaseInitializationHelper.EnsureInitializedAsync()` (Core) before querying data

**ViewModels:**
- Located in `BookLoggerApp.Core/ViewModels/`, all inherit `ViewModelBase`
- Content: `BookListViewModel`, `BookDetailViewModel`, `BookEditViewModel`, `BookshelfViewModel`, `WishlistViewModel`, `ShelfItemViewModel`
- Activity: `ReadingViewModel`, `DashboardViewModel`, `GoalsViewModel`, `StatsViewModel`
- Gamification: `PlantShopViewModel`, `DecorationShopViewModel`, `UserProgressViewModel`
- App shell: `AppStartupViewModel`, `SettingsViewModel`

**Domain Models:**
- Core entities: `Book`, `ReadingSession`, `ReadingGoal`, `UserPlant`, `PlantSpecies`, `Genre`, `BookGenre`, `Quote`, `Annotation`, `ShopItem`, `AppSettings`, `Shelf`, `BookShelf`, `PlantShelf`, `Trope`, `BookTrope`, `WishlistInfo`, `GoalExcludedBook`, `GoalGenre`, `UserDecoration`, `DecorationShelf`
- Result objects: `ProgressionResult`, `LevelUpResult`, `SessionEndResult`, `SessionSaveResult`, `BookRatingSummary`
- Share payloads: `BookShareData`, `StatsShareData` (consumed by `IShareCardService`)
- App meta: `AppUpdateState`, `ChangelogRelease`
- Onboarding: `OnboardingSnapshot`, `OnboardingMissionCatalog`, `OnboardingMissionDefinition`, `OnboardingMissionState`, `OnboardingMissionProgress`, `OnboardingMissionStatus`, `OnboardingMissionId`, `OnboardingEvent`, `OnboardingIntroStatus`, `OnboardingFeatureAtlasEntry`
- Supporting types: `RatingCategory`, `RatingCategoryInfo`, `LevelMilestone`, `PlantBoostInfo`

### Blazor UI Structure

- Components in `BookLoggerApp/Components/`
- Pages: `Books.razor`, `BookDetail.razor`, `BookEdit.razor`, `Bookshelf.razor` (includes Wishlist), `Dashboard.razor`, `Reading.razor`, `Goals.razor`, `Stats.razor`, `Settings.razor`, `PlantShop.razor`, `GettingStarted.razor`
- Layout: `MainLayout.razor`, `NavMenu.razor`, `BottomNavBar.razor`
- Shared components in `Components/Shared/` — reusable across pages:
  - Timer: `ReadingTimerInline.razor`, `QuickReadingTimer.razor` (share state via singleton `ITimerStateService`)
  - Celebrations: `BookCompletionCelebration.razor`, `LevelUpCelebration.razor`, `SessionCompleteCelebration.razor`
  - Cards: `BookCard.razor`, `StatCard.razor`, `GoalCard.razor`, `PlantCard.razor`, `PlantShopCard.razor`, `PlantDetailCard.razor`, `DecorationCard.razor`, `DecorationShopCard.razor`
  - Widgets: `PlantWidget.razor`, `UserProgressWidget.razor`
  - Onboarding: `AppStartupOverlay.razor`, `OnboardingTutorial.razor`, `GettingStartedCta.razor`
  - Modals: `DeleteConfirmationModal.razor`, `ReviewPromptModal.razor`
  - Other: `RatingInput.razor`, `GoalHeader.razor`

### JavaScript Interop

JS files in `BookLoggerApp/wwwroot/js/`:
- `bookshelfDragDrop.js` — drag-and-drop for bookshelf with long-press gesture, auto-scroll, and .NET interop
- `lazyLoading.js` — IntersectionObserver-based lazy image loading with .NET callbacks
- `animation-control.js` — pauses CSS animations when page is not visible (Page Visibility API)
- `settingsSupport.js` — helpers for the Settings page (e.g. platform links, reload)

### CSS Structure

CSS files are in `BookLoggerApp/wwwroot/css/`:
- `app.css` - Global styles, CSS variables, loading spinner, status bar safe area
- `components.css` - Book cards (spine view), stat cards, goal cards, buttons, forms
- `dashboard.css`, `stats.css`, `ratings.css`, `bookdetail.css`, `bookedit.css`, `bookshelf.css`
- `reading.css`, `reading-timer-inline.css`, `quicktimer.css`
- `plantshop.css`, `plantwidget.css`, `plant-selection.css`
- `userprogress.css`, `bottomnav.css`, `celebrations.css`, `wishlist.css`

**Mobile-First Design:**
- Breakpoints: 768px (tablet), 640px (mobile), 480px (small mobile), 400px (very small)
- Use CSS variables from `app.css` for consistent theming (cozy dark brown theme)

## CI/CD

GitHub Actions workflow (`.github/workflows/ci.yml`) runs on pushes to `main` and PRs (skips markdown/image-only changes via `paths-ignore`):
- Uses .NET 10.0.x SDK via `actions/setup-dotnet@v4`
- Copies `ApiKeys.cs.template` → `ApiKeys.cs` before restore (real keys are gitignored)
- Builds Core and Tests projects only (not Infrastructure or MAUI app — avoids platform-specific complexity)
- Runs xUnit tests with trx output
- Publishes test results using dorny/test-reporter and uploads TRX as build artifacts
- Supports `workflow_dispatch` for manual triggers

**Android release** (`.github/workflows/android-release.yml`) is triggered by pushing a `V*` tag to `main`.

## Versioning & Changelog

- Versionsschema: `V0.x.y` bis zum Play-Store-Launch, dann `V1.0.0`
- Tags werden **nur auf `main`** gesetzt (nie auf Feature-/Entwicklungsbranches)
- Release-Workflow (`.github/workflows/android-release.yml`) wird automatisch bei Tags mit `V*` ausgelöst

**CHANGELOG.md pflegen:**
Nach jeder Änderung (Feature, Bugfix, Sicherheitspatch) einen Eintrag in `CHANGELOG.md` unter dem Abschnitt `## [Unveröffentlicht]` ergänzen. **Keine neue Versionssektion anlegen** — das passiert erst beim offiziellen Release. Kategorien:
- `### Hinzugefügt` — neue Features
- `### Geändert` — Änderungen an bestehender Funktionalität
- `### Behoben` — Bugfixes
- `### Sicherheit` — Sicherheitsrelevante Fixes

## Important Notes

- Main branch for PRs: `main`
- Development uses versioned feature branches (`V1`, `V2`, ... `V6`)
- App name displayed on device: "BookHeart" (configured in `BookLoggerApp.csproj` as `ApplicationTitle`)
- Project uses latest C# language version (`<LangVersion>latest</LangVersion>`) and .NET 10
- Key NuGet packages: `ZXing.Net.Maui.Controls` (barcode scanning), `CsvHelper` (CSV import/export)
- `ApiKeys.cs` is gitignored — copy `ApiKeys.cs.template` from `BookLoggerApp.Infrastructure/Services/` and add your Google Books API key (optional, anonymous access works with rate limits)

## Codebase Knowledge Graph (Obsidian)

Es existiert ein Obsidian Vault unter `C:\Users\Tristan\Documents\Obsidian\codebase-map\`. Dieser Vault enthält eine Markdown-Datei pro Klasse, Service, Component und Page mit `[[wiki-links]]` zu Abhängigkeiten. `Index.md` im Root ist der Einstiegspunkt.

### Vault als primäre Quelle für Abhängigkeiten nutzen

**WICHTIG:** Bevor du Abhängigkeiten, Consumer oder Auswirkungen einer Änderung ermittelst, lies zuerst die zugehörige Vault-Datei (per `Read`-Tool, nicht über den Obsidian-MCP-Server). Die Vault-Dateien enthalten "Depends on"- und "Used by"-Sektionen mit allen Verknüpfungen. Erst wenn die Vault-Datei veraltet oder unvollständig erscheint, greife auf Grep/Glob im Quellcode zurück.

### Ordnerstruktur

| Ordner | Inhalt |
|---|---|
| `Models/` | Domain-Entities, DTOs, Result-Objekte, Onboarding-Models |
| `Enums/` | ReadingStatus, GoalType, PlantStatus, etc. |
| `Helpers/` | XpCalculator, SpineColorHelper, etc. |
| `Exceptions/` | BookLoggerException-Hierarchie |
| `Validators/` | FluentValidation-Validators |
| `Services/Interfaces/` | Alle `I*Service`-Abstractions |
| `Services/Implementations/` | Infrastructure-Service-Implementierungen |
| `Services/MAUI/` | Plattform-spezifische Services (MAUI-Projekt) |
| `Repositories/` | Generic + spezifische Repositories |
| `ViewModels/` | Alle ViewModels |
| `Components/Pages/` | Blazor-Seiten (*.razor.md) |
| `Components/Layout/` | MainLayout, NavMenu, BottomNavBar |
| `Components/Shared/` | Wiederverwendbare UI-Komponenten |
| `Infrastructure/Data/` | AppDbContext, Factory, DbInitializer |
| `Infrastructure/Configurations/` | EF Core Entity-Konfigurationen |
| `Infrastructure/SeedData/` | Plant-, Trope-, Decoration-Seeds |

### Regeln — Graph aktuell halten

- **Neue Klasse/Service/Component/Page erstellt →** Erstelle eine passende `.md`-Datei im richtigen Vault-Ordner.
- **Bestehende Klasse geändert** (neue Dependency, neue Methode, Umbenennung) **→** Aktualisiere die zugehörige Vault-Datei.
- **Klasse gelöscht →** Lösche auch die Vault-Datei.
- **Bidirektionale Links pflegen:** Wenn A von B abhängt, steht B in A's "Depends on" UND A in B's "Used by".
- **Index.md aktualisieren:** Bei neuen oder gelöschten Dateien den Eintrag in `Index.md` ergänzen bzw. entfernen.
- **Nach jeder Vault-Änderung pushen:**
  ```bash
  cd C:\Users\Tristan\Documents\Obsidian\codebase-map && git add . && git commit -m "Update graph" && git push
  ```

### Format pro Datei

```
# ClassName
Namespace: `Full.Namespace`
Zweck: Kurze Beschreibung

## Properties
- `PropertyName` (Type) — Beschreibung

## Methods
- `MethodName()` → Kurze Beschreibung

## Depends on
- [[Dependency1]]
- [[Dependency2]]

## Used by
- [[Consumer1]]
```

Für Blazor-Components zusätzlich: `## Parameters`, `## Injected Services`, `## Child Components`.
Für Service-Implementierungen zusätzlich: `Implements: [[IInterfaceName]]`, `Lifecycle: Singleton/Transient`.
