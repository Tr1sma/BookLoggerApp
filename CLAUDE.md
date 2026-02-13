# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BookLoggerApp (branded as **LoveLit**) is a .NET 10 MAUI Blazor Hybrid Android app for managing and tracking books. It uses Entity Framework Core with SQLite for local data storage and follows a layered architecture with Repository and Unit of Work patterns. It includes gamification features (XP/levels, plant growing, shop) — see `XP_CALCULATION_GUIDE.md` for the full progression system.

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

- `.editorconfig` enforces: UTF-8, LF line endings, 4-space indent, trim trailing whitespace
- C#: braces on new line (`csharp_new_line_before_open_brace = all`), `var` for apparent types
- System directives first, separate import groups, no unnecessary `this.` qualification
- Nullable reference types enabled
- Async methods should accept `CancellationToken` parameters
- ViewModels use `[ObservableProperty]` attribute (CommunityToolkit.Mvvm source generators)
- Testing: xUnit + FluentAssertions, arrange-act-assert pattern

## Architecture

### Project Structure

The solution follows a layered architecture with four main projects:

1. **BookLoggerApp.Core** - Domain layer (net10.0)
   - Domain models and entities
   - Service interfaces in `Services/Abstractions/`
   - ViewModels using CommunityToolkit.Mvvm
   - Helper classes: `XpCalculator`, `SpineColorHelper` in `Helpers/`
   - Custom exceptions in `Exceptions/`
   - FluentValidation validators in `Validators/`

2. **BookLoggerApp.Infrastructure** - Infrastructure layer (net10.0)
   - Entity Framework Core `AppDbContext` and entity configurations
   - Unit of Work pattern implementation (individual repositories are NOT registered in DI)
   - Service implementations in `Services/`
   - Helper: `PlantGrowthCalculator` in `Services/Helpers/`
   - Database initialization via `DbInitializer`

3. **BookLoggerApp** - Presentation layer (net10.0-android primarily)
   - Blazor components and pages in `Components/`
   - Entry point: `MauiProgram.cs` for DI configuration
   - MAUI-specific services in `Services/` (permissions, scanner, file picker, migration)
   - Platform-specific: `CustomWebChromeClient` (Android) for camera/WebView permissions

4. **BookLoggerApp.Tests** - Test project (net10.0)
   - Uses xUnit, FluentAssertions, NSubstitute (mocking), EF Core InMemory provider
   - Test helpers: `TestDbContext`, `TestDbContextFactory`, `DbContextTestHelper`
   - Mock services: `MockBookService`, `MockGoalService`, `MockPlantService`, `MockProgressionService`

### Core Architecture Patterns

**Repository + Unit of Work:**
- Generic `IRepository<T>` for common CRUD operations
- Specific repositories: `IBookRepository`, `IReadingSessionRepository`, `IReadingGoalRepository`, `IUserPlantRepository`
- Only `IUnitOfWork` is registered in DI (transient) — it creates all repositories internally with a shared DbContext
- All services should use `IUnitOfWork`, not direct repository injection

**Entity Framework Core:**
- `AppDbContext` manages entity configurations (in `Data/Configurations/`)
- SQLite provider with EF Core migrations
- Database path: `PlatformsDbPath.GetDatabasePath()` → `booklogger.db3` in `LocalApplicationData`
- Both `DbContextFactory<AppDbContext>` and direct `AppDbContext` registered as **transient**

**Dependency Injection (MauiProgram.cs):**
- `DbContext` and `DbContextFactory`: **transient**
- `IUnitOfWork`: **transient**
- Business services (BookService, ProgressService, etc.): **transient** — avoids DbContext tracking issues in Blazor
- Singletons: `IFileSystem`, `IFileSaverService`, `IBackButtonService`, `IAppSettingsProvider`, `IPermissionService`, `IScannerService`, `IShareService`, `IFilePickerService`, `IMigrationService`
- ViewModels: **transient**
- Validators: **transient**
- Registration methods: `RegisterDatabase()`, `RegisterRepositories()`, `RegisterBusinessServices()`, `RegisterViewModels()`, `RegisterValidators()`

**Service Layer:**
- Service interfaces in `BookLoggerApp.Core/Services/Abstractions/`
- Implementations in `BookLoggerApp.Infrastructure/Services/`
- Key services: `IBookService`, `IProgressService`, `IProgressionService`, `IGoalService`, `IPlantService`, `IStatsService`, `IGenreService`, `IQuoteService`, `IAnnotationService`, `IImageService`, `IImportExportService`, `IValidationService`, `INotificationService`, `ILookupService`, `IShelfService`, `IAppSettingsProvider`
- MAUI-specific (in `BookLoggerApp/Services/`): `IPermissionService`, `IScannerService`, `IShareService`, `IFilePickerService`, `IMigrationService`

**Exception Handling:**
- Custom hierarchy in `BookLoggerApp.Core/Exceptions/`: `BookLoggerException` → `EntityNotFoundException`, `ConcurrencyException`, `InsufficientFundsException`, `ValidationException`
- Global handler in `MauiProgram.cs:SetupGlobalExceptionHandler()` catches `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`

**Validation:**
- FluentValidation validators in `BookLoggerApp.Core/Validators/` for `Book`, `ReadingSession`, `ReadingGoal`, `UserPlant`
- Integrated via `IValidationService`

**Database Initialization:**
- `DbInitializer.InitializeAsync()` runs fire-and-forget in background
- ViewModels should call `DbInitializer.EnsureInitializedAsync()` before querying data

**ViewModels:**
- Located in `BookLoggerApp.Core/ViewModels/`, all inherit `ViewModelBase`
- `BookListViewModel`, `BookDetailViewModel`, `BookEditViewModel`, `BookshelfViewModel`, `ReadingViewModel`, `DashboardViewModel`, `GoalsViewModel`, `StatsViewModel`, `SettingsViewModel`, `PlantShopViewModel`, `UserProgressViewModel`, `ShelfItemViewModel`

**Domain Models:**
- Core entities: `Book`, `ReadingSession`, `ReadingGoal`, `UserPlant`, `PlantSpecies`, `Genre`, `BookGenre`, `Quote`, `Annotation`, `ShopItem`, `AppSettings`, `Shelf`, `BookShelf`, `PlantShelf`, `Trope`, `BookTrope`
- Result objects: `ProgressionResult`, `LevelUpResult`, `SessionEndResult`, `BookRatingSummary`
- Supporting types: `RatingCategory`, `RatingCategoryInfo`, `LevelMilestone`, `PlantBoostInfo`

### Blazor UI Structure

- Components in `BookLoggerApp/Components/`
- Pages: `Books.razor`, `BookDetail.razor`, `BookEdit.razor`, `Bookshelf.razor`, `Dashboard.razor`, `Reading.razor`, `Goals.razor`, `Stats.razor`, `Settings.razor`, `PlantShop.razor`
- Layout: `MainLayout.razor`, `NavMenu.razor`

### CSS Structure

CSS files are in `BookLoggerApp/wwwroot/css/`:
- `app.css` - Global styles, CSS variables, loading spinner, status bar safe area
- `components.css` - Book cards (spine view), stat cards, goal cards, buttons, forms
- `dashboard.css`, `stats.css`, `ratings.css`, `bookdetail.css`, `bookedit.css`, `bookshelf.css`
- `reading.css`, `reading-timer-inline.css`, `quicktimer.css`
- `plantshop.css`, `plantwidget.css`, `plant-selection.css`
- `userprogress.css`, `bottomnav.css`, `celebrations.css`

**Mobile-First Design:**
- Breakpoints: 768px (tablet), 640px (mobile), 480px (small mobile), 400px (very small)
- Use CSS variables from `app.css` for consistent theming (cozy dark brown theme)

## CI/CD

GitHub Actions workflow (`.github/workflows/ci.yml`) runs on pushes to `main` and PRs:
- Builds Core and Tests projects only (not Infrastructure or MAUI app — avoids platform-specific complexity)
- Runs xUnit tests with trx output
- Publishes test results using dorny/test-reporter
- **Note:** CI currently uses .NET 9.0.x SDK — update to .NET 10 when ready

## Important Notes

- Main branch for PRs: `main`
- Development uses versioned feature branches (`V1`, `V2`, ... `V5`)
- App name displayed on device: "LoveLit" (configured in `BookLoggerApp.csproj` as `ApplicationTitle`)
- Project uses latest C# language version (`<LangVersion>latest</LangVersion>`) and .NET 10
- Key NuGet packages: `ZXing.Net.Maui.Controls` (barcode scanning), `CsvHelper` (CSV import/export)
