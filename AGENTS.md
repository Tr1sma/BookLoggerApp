# Repository Guidelines

## Project Structure

`BookLoggerApp/` — MAUI Blazor Hybrid app (Presentation). Entry: `MauiProgram.cs`. Routeable UI goes in `Components/Pages/`, reusables in `Components/Shared/`, platform code in `Platforms/`, static assets in `wwwroot/` and `Resources/`.

`BookLoggerApp.Core/` — Domain layer. Framework-agnostic: `Models/`, `ViewModels/`, `Services/Abstractions/`, `Validators/`, `Helpers/`, `Exceptions/`.

`BookLoggerApp.Infrastructure/` — Persistence and integrations. EF Core `Data/` (includes `AppDbContextFactory` for design-time), `Migrations/`, `Repositories/`, concrete `Services/`.

`BookLoggerApp.Tests/` — xUnit + FluentAssertions + NSubstitute + EF Core InMemory. Organized by concern: `Unit/`, `Integration/`, `Services/`, `Repositories/`, `Security/`, `Models/`, plus `TestHelpers/`.

## Build, Test, and Development Commands

```bash
# Restore (repo root)
dotnet restore

# Build solution
dotnet build BookLoggerApp.sln

# Build and run Android target
dotnet build BookLoggerApp/BookLoggerApp.csproj -f net10.0-android -t:Run

# Run tests
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj

# Run specific test
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~DashboardViewModel"
```

**CI Note:** CI only builds `BookLoggerApp.Core` and `BookLoggerApp.Tests` (not Infrastructure or the MAUI app) to avoid platform-specific dependencies. See `.github/workflows/ci.yml`.

## EF Core Migrations

```bash
# Create migration
dotnet ef migrations add <Name> --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp

# Apply migration
dotnet ef database update --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp
```

## Coding Style

Follow `.editorconfig`: UTF-8, LF, 4-space indent, trim trailing whitespace, final newline.

- Braces on new lines (`csharp_new_line_before_open_brace = all`)
- `var` for built-in/apparent types only (`csharp_style_var_elsewhere = false`)
- System directives first, separate import groups
- No `this.` qualification
- PascalCase for types, Razor components, filenames (e.g., `BookDetail.razor`, `BookServiceTests.cs`)
- Interface prefix `I`
- Nullable annotations enabled
- Async methods accept `CancellationToken`

## Architecture Notes

**DI Registration (MauiProgram.cs):**
- `DbContext`, `DbContextFactory`, `IUnitOfWork`: **transient**
- Business services: **transient** (avoids DbContext tracking issues in Blazor)
- `ILookupService`: registered via `AddHttpClient<>` (typed HttpClient)
- Singletons: `IFileSystem`, `IFileSaverService`, `IBackButtonService`, `IAppSettingsProvider`, `IPermissionService`, `IScannerService`, `IShareService`, `IFilePickerService`, `IMigrationService`, `ITimerStateService`

**Repository + Unit of Work:**
- Only `IUnitOfWork` is registered in DI (transient). It creates repositories internally with a shared DbContext.
- Services use `IUnitOfWork`, not direct repository injection.

**Database:**
- Path: `booklogger.db3` in `LocalApplicationData` (via `PlatformsDbPath.GetDatabasePath()`)
- `DatabaseMigrationHelper.MigrateIfNecessary()` runs before DbContext registration to handle legacy path migration

**Google Books API:**
- `LookupService` queries `googleapis.com/books/v1/volumes` with 3 retries (1s, 3s, 6s delays)
- API key via `BookLoggerApp.Infrastructure/Services/ApiKeys.cs` (gitignored — copy from `ApiKeys.cs.template` for local dev)

## Testing Guidelines

- Name files `SubjectTests.cs`
- Method names like `AddAsync_ShouldSetDateAdded`
- Use `// Arrange`, `// Act`, `// Assert` blocks
- Test helpers: `TestDbContext`, `TestDbContextFactory`, `DbContextTestHelper`

## Changelog Guidelines

Add entries to `CHANGELOG.md` under `## [Unveröffentlicht]` only. **Never create a new version section** — that happens only on release tags.

Categories:
- `### Hinzugefügt` — new features
- `### Geändert` — behavior changes
- `### Behoben` — bug fixes
- `### Sicherheit` — security fixes

One bullet per logical change. Skip refactors unless they affect behavior.

Version schema: `V0.x.y` pre-release, `V1.0.0` for first Play Store release.

## Commit & PR Guidelines

- Short imperative subjects: `Fix goal progress bug...`, `Added Notifications...`, `Fixed Issue #159...`
- PRs against `main`
- Include validation steps: `dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj`
- Schema changes must include migration files

## Security

Do not commit real API keys. Copy `ApiKeys.cs.template` to `ApiKeys.cs` for local credentials.

Review `SECURITY.md` when touching import/export, file handling, or image-processing code.
