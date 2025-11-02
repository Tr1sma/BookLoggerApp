# Repository Guidelines

## Project Structure & Modules
- `BookLoggerApp/` — .NET MAUI UI app (XAML, pages, components, resources). Targets `net9.0-*` platforms including Windows, Android, iOS, MacCatalyst.
- `BookLoggerApp.Core/` — domain logic: `Models/`, `Services/`, `Infrastructure/`, `ViewModels/`. Keep UI‑agnostic code here.
- `BookLoggerApp.Tests/` — xUnit tests with FluentAssertions mirroring Core structure (`Services/`, `ViewModels/`).
- Root: `BookLoggerApp.sln`, GitHub workflows, `.editorconfig`, licenses, docs.

## Build, Test, Run
- Restore workloads (first time): `dotnet workload install maui`.
- Build solution: `dotnet build BookLoggerApp.sln -c Debug`.
- Run tests: `dotnet test BookLoggerApp.sln -c Debug`.
- Run Windows app: `dotnet build -t:Run -f net9.0-windows10.0.19041.0 BookLoggerApp/`.
- Run Android (device/emulator): `dotnet build -t:Run -f net9.0-android BookLoggerApp/`.

## Coding Style & Naming
- Follow `.editorconfig`: UTF‑8, LF, spaces, 4‑space indent; sort `using` with `System` first; prefer `var` when type is apparent; enable nullability.
- C#: PascalCase for types/methods; camelCase for locals/fields; suffix `Service`, `ViewModel`, `Repository` as appropriate.
- Keep UI out of Core; inject dependencies into services/view models.

## Testing Guidelines
- Frameworks: xUnit + FluentAssertions.
- File naming: `*Tests.cs`; mirror Core namespaces/paths (e.g., `BookLoggerApp.Tests/Services/BookServiceTests.cs`).
- Style: Arrange‑Act‑Assert; prefer fluent assertions (`result.Should().Be(...)`).
- Run locally: `dotnet test`; add tests for new logic, especially in Core and ViewModels.

## Commit & PRs
- Commits: imperative, concise subject; optional scope. Examples: `Add progress service`, `Fix null handling in Book model`, `Update CI matrix`.
- PRs: clear description, link issues (`Closes #123`), screenshots/gifs for UI changes, list test coverage/steps, and platform(s) verified.
- CI should be green; include tests for bug fixes/features.

## Security & Config
- Do not commit secrets or API keys; avoid storing credentials in source files. Use platform keychains/secure storage per MAUI guidance.
- Platform SDKs: ensure required SDKs/emulators installed for targeted frameworks.
