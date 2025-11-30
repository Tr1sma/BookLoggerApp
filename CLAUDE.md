# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BookLoggerApp is a .NET 10 MAUI Blazor Hybrid Android app for managing and tracking books. It uses Entity Framework Core with SQLite for local data storage and follows a layered architecture with Repository and Unit of Work patterns.

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

## Architecture

### Project Structure

The solution follows a layered architecture with four main projects:

1. **BookLoggerApp.Core** - Domain layer (net10.0)
   - Domain models and entities
   - Service interfaces in `Services/Abstractions/`
   - ViewModels using CommunityToolkit.Mvvm
   - Custom exceptions in `Exceptions/`
   - FluentValidation validators in `Validators/`

2. **BookLoggerApp.Infrastructure** - Infrastructure layer (net10.0)
   - Entity Framework Core `AppDbContext` and entity configurations
   - Repository implementations (generic and specific)
   - Unit of Work pattern implementation
   - Service implementations in `Services/`
   - Database initialization via `DbInitializer`

3. **BookLoggerApp** - Presentation layer (multi-targeted)
   - Targets: net10.0-android, net10.0-ios, net10.0-maccatalyst, net10.0-windows
   - Blazor components and pages in `Components/`
   - Entry point: `MauiProgram.cs` for DI configuration
   - Platform-specific implementations in `Platforms/`

4. **BookLoggerApp.Tests** - Test project (net10.0)
   - Uses xUnit as test framework
   - Uses FluentAssertions for assertions
   - Uses EF Core InMemory provider for database tests

### Core Architecture Patterns

**Layered Architecture:**
- Domain layer (Core) contains business logic and is framework-agnostic
- Infrastructure layer implements data access and external dependencies
- Presentation layer (MAUI Blazor) contains UI and user interaction logic

**Repository Pattern:**
- Generic `IRepository<T>` for common CRUD operations
- Specific repositories for complex queries: `IBookRepository`, `IReadingSessionRepository`, `IReadingGoalRepository`, `IUserPlantRepository`
- All repositories located in `BookLoggerApp.Infrastructure/Repositories/`

**Unit of Work Pattern:**
- `IUnitOfWork` coordinates repositories and manages transactions
- Ensures consistency across multiple repository operations
- Implemented in `BookLoggerApp.Infrastructure/Repositories/UnitOfWork.cs`

**Entity Framework Core:**
- `AppDbContext` manages entity configurations
- Entity configurations in `BookLoggerApp.Infrastructure/Data/Configurations/`
- Uses SQLite provider with EF Core migrations
- Database path resolved via `PlatformsDbPath.GetDatabasePath()` in `BookLoggerApp.Infrastructure/PlatformsDbPath.cs`
- Default database file: `booklogger.db3` in `Environment.SpecialFolder.LocalApplicationData`

**Dependency Injection:**
- All services registered in `MauiProgram.cs:CreateMauiApp()`
- Services are registered as **singletons** (shares DbContext and UnitOfWork for transaction consistency)
- ViewModels are registered as **transient**
- Validators are registered as **transient**
- Registration methods: `RegisterDatabase()`, `RegisterRepositories()`, `RegisterBusinessServices()`, `RegisterViewModels()`, `RegisterValidators()`

**Service Layer:**
- Service interfaces defined in `BookLoggerApp.Core/Services/Abstractions/`
- Service implementations in `BookLoggerApp.Infrastructure/Services/`
- Key services include:
  - `IBookService` - Book CRUD operations
  - `IProgressService` - Reading session tracking
  - `IProgressionService` - Level and XP progression system
  - `IGoalService` - Reading goals management
  - `IPlantService` - Gamification plant system
  - `IStatsService` - Statistics and analytics
  - `IGenreService` - Genre management
  - `IQuoteService`, `IAnnotationService` - Book annotations
  - `IImageService` - Image handling
  - `IImportExportService` - Data import/export
  - `IValidationService` - FluentValidation integration
  - `INotificationService` - Notifications
  - `ILookupService` - External book API lookups
  - `IAppSettingsProvider` - App configuration

**Exception Handling:**
- Custom exception hierarchy in `BookLoggerApp.Core/Exceptions/`
- Base exception: `BookLoggerException`
- Specific exceptions: `EntityNotFoundException`, `ConcurrencyException`, `InsufficientFundsException`, `ValidationException`
- Global exception handler configured in `MauiProgram.cs:SetupGlobalExceptionHandler()`
- Handles both `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`

**Validation:**
- Uses FluentValidation for model validation
- Validators in `BookLoggerApp.Core/Validators/`
- Validators for: `Book`, `ReadingSession`, `ReadingGoal`, `UserPlant`
- Integrated via `IValidationService`

**Database Initialization:**
- Async initialization via `DbInitializer.InitializeAsync()` in `MauiProgram.cs:InitializeDatabase()`
- Runs fire-and-forget in background task
- Creates database schema and seeds initial data

**ViewModels:**
- Located in `BookLoggerApp.Core/ViewModels/`
- All ViewModels inherit from `ViewModelBase`
- ViewModels: `BookListViewModel`, `BookDetailViewModel`, `BookEditViewModel`, `BookshelfViewModel`, `ReadingViewModel`, `DashboardViewModel`, `GoalsViewModel`, `StatsViewModel`, `SettingsViewModel`, `PlantShopViewModel`, `UserProgressViewModel`
- Use CommunityToolkit.Mvvm for observable properties and commands

**Domain Models:**
- Core entities: `Book`, `ReadingSession`, `ReadingGoal`, `UserPlant`, `PlantSpecies`, `Genre`, `BookGenre`, `Quote`, `Annotation`, `ShopItem`, `AppSettings`
- Result objects: `ProgressionResult`, `LevelUpResult`, `SessionEndResult`, `BookRatingSummary`
- Supporting types: `RatingCategory`, `LevelMilestone`, `PlantBoostInfo`

### Blazor UI Structure

- Components in `BookLoggerApp/Components/`
- Pages: `Books.razor`, `BookDetail.razor`, `BookEdit.razor`, `Bookshelf.razor`, `Dashboard.razor`, `Reading.razor`, `Goals.razor`, `Stats.razor`, `Settings.razor`, `PlantShop.razor`
- Layout: `MainLayout.razor`, `NavMenu.razor`
- Routing defined in `Routes.razor`

## CI/CD

GitHub Actions workflow (`.github/workflows/ci.yml`) runs on pushes to `main` and PRs:
- Builds Core and Tests projects only (not Infrastructure or MAUI app)
- Runs xUnit tests with trx output
- Publishes test results using dorny/test-reporter
- **Note:** CI currently uses .NET 9.0.x SDK - update to .NET 10 when merging migration branch

## Important Notes

- The MAUI app project and Infrastructure project are NOT built in CI (avoids MAUI platform-specific complexity)
- All services are Singletons sharing the same DbContext for transaction consistency
- Database initialization is fire-and-forget but provides `DbInitializer.EnsureInitializedAsync()` for ViewModels to await
- Main branch for PRs: `main`
- Development branch: `dev`
- Project uses latest C# language version (`<LangVersion>latest</LangVersion>`) and .NET 10
