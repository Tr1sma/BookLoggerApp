# Repository Guidelines

## Project Structure & Module Organization
`BookLoggerApp/` is the MAUI Blazor Hybrid app. Put routeable UI in `Components/Pages/`, reusable pieces in `Components/Shared/`, platform code in `Platforms/`, and static assets in `wwwroot/` and `Resources/`.

`BookLoggerApp.Core/` holds framework-agnostic domain code: `Models/`, `ViewModels/`, `Services/Abstractions/`, `Validators/`, and `Helpers/`.

`BookLoggerApp.Infrastructure/` contains persistence and integration code: EF Core `Data/`, `Migrations/`, `Repositories/`, and concrete `Services/`.

`BookLoggerApp.Tests/` is organized by concern: `Unit/`, `Integration/`, `Services/`, `Repositories/`, `Security/`, `Models/`, plus shared fixtures in `TestHelpers/`.

## Build, Test, and Development Commands
`dotnet restore` restores all projects from the repo root.

`dotnet build BookLoggerApp.sln` builds the solution.

`dotnet build BookLoggerApp/BookLoggerApp.csproj -f net10.0-android -t:Run` builds and launches the Android target.

`dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj` runs the xUnit suite.

`dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~DashboardViewModel"` runs a focused subset.

`dotnet ef migrations add <Name> --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp` creates a migration. Replace `add` with `database update` to apply it.

## Coding Style & Naming Conventions
Follow `.editorconfig`: UTF-8, LF, 4-space indentation, trimmed trailing whitespace, and a final newline. C# uses braces on new lines and `var` only when the type is built-in or obvious. Keep nullable annotations enabled.

Use PascalCase for types, Razor components, and filenames such as `BookDetail.razor` and `BookServiceTests.cs`. Prefix interfaces with `I`. Keep business rules in Core or Infrastructure, not embedded in Razor markup.

## Testing Guidelines
Tests use xUnit, FluentAssertions, NSubstitute, and EF Core InMemory. Add new tests beside the matching concern and name files `SubjectTests`.

Prefer method names like `AddAsync_ShouldSetDateAdded` and keep `// Arrange`, `// Act`, `// Assert` blocks. There is no coverage gate in CI, so cover service, repository, and security regressions for every behavior change.

## Changelog Guidelines

After every change — whether feature, bugfix, or security patch — add an entry to `CHANGELOG.md` under the `## [Unveröffentlicht]` section. **Never create a new version section** — that only happens on an official release tag. Use these categories:

- `### Hinzugefügt` for new functionality
- `### Geändert` for changes to existing behavior
- `### Behoben` for bug fixes
- `### Sicherheit` for security-relevant fixes

Keep entries user-facing and concise. One bullet per logical change. Skip internal refactors or dependency bumps unless they affect behavior.

## Commit & Pull Request Guidelines
Recent commits favor short imperative subjects such as `Fix goal progress bug...`, `Added Notifications...`, and issue-linked messages like `Fixed Issue #159...`. Follow that pattern: one clear subject, optional issue reference, and no placeholder messages.

Open PRs against `main`. Include a short summary, linked issue, validation steps such as `dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj`, and screenshots for Razor or CSS changes. Keep PRs scoped; schema changes should include migration files.

## Security & Configuration Tips
Do not commit real API keys. Copy `BookLoggerApp.Infrastructure/Services/ApiKeys.cs.template` to `ApiKeys.cs` for local Google Books credentials. Review `SECURITY.md` when changing import/export, file handling, or image-processing code.
