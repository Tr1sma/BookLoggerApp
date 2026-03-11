# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BookLoggerApp (branded as **BookHeart**) is a .NET 10 MAUI Blazor Hybrid Android app for managing and tracking books. It uses Entity Framework Core with SQLite for local data storage and follows a layered architecture with Repository and Unit of Work patterns. It includes gamification features (XP/levels, plant growing, shop) — see `XP_CALCULATION_GUIDE.md` for the full progression system.

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

1. **BookLoggerApp.Core** (`net10.0`) — Domain layer: models, enums, service interfaces (`Services/Abstractions/`), ViewModels, validators, helpers, custom exceptions
2. **BookLoggerApp.Infrastructure** (`net10.0`) — Data layer: EF Core `AppDbContext` + configurations, migrations, seed data, Unit of Work + repositories, service implementations
3. **BookLoggerApp** (`net10.0-android`) — Presentation layer: Blazor components/pages, MAUI-specific services, JS interop, CSS
4. **BookLoggerApp.Tests** (`net10.0`) — xUnit + FluentAssertions + NSubstitute + EF Core InMemory

### Critical Patterns

**Database Initialization (two-phase):**
1. `DatabaseMigrationHelper.MigrateIfNecessary()` runs synchronously in `MauiProgram.RegisterDatabase()` BEFORE DbContext registration — handles legacy DB path migration with XP-based comparison
2. `DbInitializer.InitializeAsync()` runs fire-and-forget via `Task.Run()` after app starts
3. **ViewModels MUST gate data access** via `ViewModelBase.ExecuteSafelyWithDbAsync()` which calls `DatabaseInitializationHelper.EnsureInitializedAsync()` before executing. This prevents race conditions with the fire-and-forget initialization.

**ViewModelBase pattern:**
- All ViewModels inherit `ViewModelBase : ObservableObject`
- Provides `IsBusy`, `ErrorMessage` observable properties
- `ExecuteSafelyAsync()` — wraps with IsBusy/error management + exception handling
- `ExecuteSafelyWithDbAsync()` — same but adds the database initialization gate (use this for any DB access)

**Repository + Unit of Work:**
- Only `IUnitOfWork` is registered in DI (transient) — it creates repositories internally via lazy initialization (`_books ??= new BookRepository(_context)`)
- Services inject `IUnitOfWork`, NEVER individual repositories
- `IUnitOfWork` exposes transaction management: `BeginTransactionAsync()`, `CommitAsync()`, `RollbackAsync()`
- Direct `Context` property available for complex LINQ queries

**Dependency Injection lifetimes (MauiProgram.cs):**
- **Transient**: DbContext, DbContextFactory, IUnitOfWork, all business services, ViewModels, validators — this avoids DbContext tracking issues across Blazor component lifecycles
- **Typed HttpClient**: `ILookupService` via `AddHttpClient<>()` (10s timeout, built-in retry: 1s/3s/6s)
- **Singletons**: Platform services (`IPermissionService`, `IScannerService`, `ITimerStateService`, etc.) and `IAppSettingsProvider`
- Registration organized into methods: `RegisterDatabase()`, `RegisterRepositories()`, `RegisterBusinessServices()`, `RegisterViewModels()`, `RegisterValidators()`

**Service Layer:**
- Interfaces in `BookLoggerApp.Core/Services/Abstractions/`, implementations in `BookLoggerApp.Infrastructure/Services/`
- MAUI-specific services (interfaces in Core, implementations in `BookLoggerApp/Services/`): `IPermissionService`, `IScannerService`, `IShareService`, `IFilePickerService`, `IMigrationService`, `ITimerStateService`
- All async methods follow `CancellationToken ct = default` convention (441+ occurrences)

**Exception hierarchy** (`BookLoggerApp.Core/Exceptions/`):
- `BookLoggerException` → `EntityNotFoundException`, `ConcurrencyException`, `InsufficientFundsException`, `ValidationException`

### Platform-Specific Code

- `#if ANDROID` guards used in `MauiProgram.cs` (BlazorWebViewHandler for camera permissions) and `NotificationService.cs` (Android 12+ exact alarm permission)
- For future platform-specific services, use pattern: `#if ANDROID { real impl } #else { no-op fallback } #endif`

### Blazor UI

- Pages in `BookLoggerApp/Components/Pages/`, shared components in `Components/Shared/`
- Layout: `MainLayout.razor`, `NavMenu.razor`, `BottomNavBar.razor`
- Timer state shared across components via singleton `ITimerStateService`
- JS interop in `wwwroot/js/`: drag-and-drop, lazy loading, animation control
- Mobile-first CSS in `wwwroot/css/` with cozy dark brown theme; use CSS variables from `app.css`

### Google Books API

- `LookupService` queries `googleapis.com/books/v1/volumes` for ISBN lookup and search
- API key in `BookLoggerApp.Infrastructure/Services/ApiKeys.cs` (gitignored) — copy from `ApiKeys.cs.template`. Anonymous access works with rate limits.

## Testing Gotchas

- **Target framework mismatch**: Test project targets `net10.0` while MAUI targets `net10.0-android` — MAUI-layer services cannot be directly referenced, only mocked via NSubstitute or custom mock classes
- **Custom mocks**: `TestHelpers/` contains mock services (`MockBookService`, etc.) that use in-memory `Dictionary<Guid, T>` for stateful testing. Use these for ViewModel unit tests; use NSubstitute for simpler interface stubs.
- **Test DB**: `TestDbContext.Create()` provides in-memory SQLite contexts; `TestDbContextFactory` implements `IDbContextFactory<AppDbContext>` for tests
- **FluentAssertions 8.x**: Use `BeGreaterThanOrEqualTo` (not `BeGreaterOrEqualTo`)
- **Validation**: FluentValidation validators in `BookLoggerApp.Core/Validators/` integrated via `IValidationService`

## CI/CD

GitHub Actions workflow (`.github/workflows/ci.yml`):
- Builds Core and Tests projects only (not Infrastructure or MAUI — avoids platform-specific complexity)
- Copies `ApiKeys.cs.template` → `ApiKeys.cs` before build
- Runs xUnit tests with trx output, publishes via dorny/test-reporter
- Triggers on pushes to `main`, PRs, and manual `workflow_dispatch`
- Skips markdown/image-only changes via `paths-ignore`

## Important Notes

- Main branch for PRs: `main`
- Development uses versioned feature branches (`V1`, `V2`, ... `V6`)
- App name on device: "BookHeart" (configured as `ApplicationTitle` in `BookLoggerApp.csproj`)
- `<LangVersion>latest</LangVersion>` with .NET 10
- Key NuGet packages: `ZXing.Net.Maui.Controls` (barcode scanning), `CsvHelper` (CSV import/export)
- Monetization roadmap (subscriptions, billing, ads) is planned in `ToDos.md` but not yet implemented
