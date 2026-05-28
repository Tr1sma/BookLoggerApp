# AGENTS.md

See `CLAUDE.md` for comprehensive project documentation including architecture, patterns, templates, and localization.

## Cursor Cloud specific instructions

### Environment

- **.NET 10 SDK** is installed at `/usr/share/dotnet`. The update script handles installation if missing.
- `ApiKeys.cs` is gitignored. The update script copies the template automatically. No real API key is needed — anonymous Google Books access works with rate limits.

### Building

This is a .NET MAUI Blazor Hybrid **Android** app. In the Cloud Agent VM (headless Linux), only the non-MAUI projects can be built and tested:

```bash
# Build the three non-MAUI projects (matches CI)
dotnet build BookLoggerApp.Core/BookLoggerApp.Core.csproj
dotnet build BookLoggerApp.Infrastructure/BookLoggerApp.Infrastructure.csproj
dotnet build BookLoggerApp.Tests/BookLoggerApp.Tests.csproj
```

The MAUI project (`BookLoggerApp/BookLoggerApp.csproj`) targets `net10.0-android` and requires the Android SDK workload — it **cannot** be built or run in the Cloud Agent VM. This matches the CI workflow which also only builds Core + Tests.

### Testing

```bash
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj
```

All tests use EF Core InMemory provider and require no external services or network access. The test suite currently has 1,237 tests.

### Lint

No standalone linter is configured. Build warnings serve as the lint mechanism — `TreatWarningsAsErrors` is `false` in all projects. Existing warnings in the codebase (MVVMTK0034, CS0414, CA2000) are known and accepted.

### Key gotchas

- The `.editorconfig` enforces LF line endings. Ensure any new files use LF, not CRLF.
- Tests pin `CultureInfo` to `InvariantCulture` via a `[ModuleInitializer]` in `TestHelpers/TestStringLocalizer.cs` to avoid locale-dependent failures.
- `DatabaseInitializationHelper.MarkAsInitialized()` must be called in ViewModel test constructors — see patterns in `CLAUDE.md`.
