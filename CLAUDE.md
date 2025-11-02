# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BookLoggerApp is a .NET 9 MAUI Blazor Hybrid Android app for managing and tracking books. It uses SQLite for local data storage and follows MVVM architecture with dependency injection.

## Build and Test Commands

### Building the solution
```bash
# Build the entire solution
dotnet build BookLoggerApp.sln

# Build individual projects
dotnet build BookLoggerApp.Core/BookLoggerApp.Core.csproj -c Release
dotnet build BookLoggerApp.Tests/BookLoggerApp.Tests.csproj -c Release
dotnet build BookLoggerApp/BookLoggerApp.csproj -c Release
```

### Running tests
```bash
# Run all tests
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj

# Run tests with detailed output
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj -c Release --logger "trx;LogFileName=test_results.trx"

# Run specific test
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~YourTestName"
```

### Restore packages
```bash
dotnet restore BookLoggerApp.Core/BookLoggerApp.Core.csproj
dotnet restore BookLoggerApp.Tests/BookLoggerApp.Tests.csproj
dotnet restore BookLoggerApp/BookLoggerApp.csproj
```

## Architecture

### Project Structure

The solution consists of three main projects:

1. **BookLoggerApp.Core** - Core business logic layer (net9.0)
   - Contains domain models, services, and ViewModels
   - Platform-agnostic shared code
   - Uses CommunityToolkit.Mvvm for MVVM support
   - Uses sqlite-net-pcl for database operations

2. **BookLoggerApp** - MAUI Blazor Hybrid UI layer (multi-targeted)
   - Targets: Android, iOS, macOS Catalyst, Windows
   - Contains Blazor components and pages
   - Platform-specific implementations in `Platforms/` folder
   - Entry point: `MauiProgram.cs` for DI configuration

3. **BookLoggerApp.Tests** - Unit test project (net9.0)
   - Uses xUnit as test framework
   - Uses FluentAssertions for assertions

### Core Architecture Patterns

**Service Layer Pattern:**
- Services are defined via interfaces in `BookLoggerApp.Core/Services/Abstractions/`
- Two implementation strategies exist side-by-side:
  - `InMemory*Service` - In-memory implementations (for testing/prototyping)
  - `Sqlite*Service` - SQLite-backed implementations (production)
- Key services:
  - `IBookService` - CRUD operations for books
  - `IProgressService` - Tracks reading sessions and aggregates progress

**Dependency Injection:**
- All services and ViewModels are registered in `MauiProgram.cs:CreateMauiApp()`
- Services are singletons, ViewModels are transient
- SQLite connection is shared via `SQLiteAsyncConnection` singleton

**Database Path Resolution:**
- Cross-platform database path handled by `PlatformsDbPath.GetDatabasePath()` in `BookLoggerApp.Core/Infrastructure/PlatformsDbPath.cs`
- Uses `Environment.SpecialFolder.LocalApplicationData` for platform-safe app data directory
- Default database file: `booklogger.db3`

**ViewModels:**
- Located in `BookLoggerApp.Core/ViewModels/`
- `BookListViewModel` - Manages book list display
- `BookDetailViewModel` - Manages individual book details and reading sessions
- ViewModels use CommunityToolkit.Mvvm for observable properties and commands

**Models:**
- `Book` - Core book entity with Id, Title, Author, Status (Planned/Reading/Completed/Abandoned)
- `ReadingSession` - Tracks individual reading sessions with BookId, StartedAt, Minutes, PagesRead

### Blazor UI Structure

- Components are in `BookLoggerApp/Components/`
- Pages: `Books.razor` (list view), `BookDetail.razor` (detail/edit view)
- Layout: `MainLayout.razor`, `NavMenu.razor`
- Routing defined in `Routes.razor`

## CI/CD

GitHub Actions workflow (`.github/workflows/ci.yml`) runs on pushes to `main`:
- Builds Core and Tests projects (not the full MAUI app)
- Runs xUnit tests with trx output
- Publishes test results using dorny/test-reporter
- Only builds/tests Core and Tests projects to avoid MAUI platform-specific build complexity in CI

## Important Notes

- The MAUI app project itself is NOT built in CI - only Core and Tests projects
- Database initialization happens in service constructors (fire-and-forget in MauiProgram.cs)
- Main branch for PRs: `main`
- Development branch: `dev`
- Project uses latest C# language version and .NET 9
