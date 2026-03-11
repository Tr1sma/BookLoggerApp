# GEMINI.md - BookLoggerApp (BookHeart)

This file provides comprehensive guidance for Gemini CLI when working within the **BookLoggerApp** (branded as **BookHeart**) repository.

## Project Overview

**BookLoggerApp** is a modern .NET 10 MAUI Blazor Hybrid Android application designed for managing and tracking personal libraries with strong gamification elements.

### Core Technologies
- **Framework:** .NET 10 MAUI Blazor Hybrid
- **Language:** C# 13, HTML/CSS (Razor Components)
- **Database:** SQLite with Entity Framework Core 10
- **Architecture:** Clean Layered Architecture
- **State Management:** MVVM (CommunityToolkit.Mvvm)
- **Testing:** xUnit, FluentAssertions, NSubstitute

### Key Features
- **Library Management:** Comprehensive book tracking (Status: Planned, Reading, Completed, Abandoned, Wishlist).
- **Gamification:** XP/Level system, Virtual Garden (growing plants based on reading time), In-game Shop.
- **Advanced Rating:** Multi-category rating system (Characters, Plot, Style, Spice, Pacing, World Building).
- **Analytics:** Detailed reading statistics, trends, and genre analysis.
- **Offline-First:** All data stored locally in SQLite with JSON/CSV import/export.

---

## Architecture & Project Structure

The solution follows a clean, layered architecture:

1.  **BookLoggerApp.Core (`net10.0`):** Domain Layer.
    - `Models/`: Entity definitions (Book, ReadingSession, etc.).
    - `ViewModels/`: UI logic using MVVM pattern.
    - `Services/Abstractions/`: Domain service interfaces.
    - `Validators/`: FluentValidation rules.
    - `Exceptions/`: Custom exception hierarchy (`BookLoggerException`).
2.  **BookLoggerApp.Infrastructure (`net10.0`):** Data & Implementation Layer.
    - `Data/`: EF Core `AppDbContext`, Migrations, Seed Data.
    - `Repositories/`: Unit of Work and Repository implementations.
    - `Services/`: Concrete service implementations.
3.  **BookLoggerApp (`net10.0-android`):** Presentation Layer (MAUI + Blazor).
    - `Components/Pages/`: UI Screens (Razor).
    - `Platforms/`: Native Android implementations (e.g., Camera, Notifications).
    - `wwwroot/`: Static assets (CSS, JS, Images).
4.  **BookLoggerApp.Tests (`net10.0`):** Testing Layer.
    - Unit and integration tests using xUnit and FluentAssertions.

---

## Build, Run, and Test Commands

### General
```powershell
# Restore dependencies
dotnet restore

# Build the entire solution
dotnet build BookLoggerApp.sln

# Run all tests
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj
```

### Running the App (Android)
```powershell
# Build and run on Android emulator or device
dotnet build -f net10.0-android -t:Run
```

### EF Core Migrations
Always run migration commands from the solution root:
```powershell
# Add a new migration
dotnet ef migrations add <MigrationName> --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp

# Update the database
dotnet ef database update --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp

# Remove last migration
dotnet ef migrations remove --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp
```

---

## Development Conventions & Patterns

### 1. ViewModel & Database Safety
All ViewModels inherit from `ViewModelBase`.
- **Busy State:** Use `IsBusy` property to manage loading indicators.
- **Error Handling:** Use `ErrorMessage` for UI-bound error reporting.
- **Safe DB Execution:** **CRITICAL:** Always use `ExecuteSafelyWithDbAsync()` for any database operations. This ensures the database is fully initialized (migrations/seeding) before access.

```csharp
await ExecuteSafelyWithDbAsync(async () => {
    var books = await _bookService.GetAllBooksAsync();
    // ... logic
});
```

### 2. Dependency Injection (DI)
- **DbContext:** Registered as `Transient` via `AddDbContextFactory` to avoid concurrency issues in Blazor.
- **Unit of Work:** Use `IUnitOfWork` (Transient) instead of direct repository injection.
- **Services:** Business services are generally `Transient`.
- **Platform Services:** Services requiring native interaction (Scanner, Permissions) are `Singleton`.

### 3. Coding Style
- **Async/Await:** All async methods must accept a `CancellationToken ct = default`.
- **MVVM:** Use `[ObservableProperty]` for properties to leverage source generators.
- **Directives:** Keep `System` namespaces first. No `this.` qualification unless necessary.
- **Nullable:** Nullable reference types are enabled; respect them.

### 4. Testing Patterns
- **Mocking:** Use `NSubstitute` for simple interface mocking.
- **Stateful Mocks:** Use custom mock classes in `BookLoggerApp.Tests/TestHelpers/` for complex stateful service behavior.
- **DB Tests:** Use `TestDbContext.Create()` for in-memory SQLite testing.

---

## Critical Files
- `BookLoggerApp/MauiProgram.cs`: Composition root and DI registration.
- `BookLoggerApp.Infrastructure/Data/AppDbContext.cs`: Database schema and seeding.
- `BookLoggerApp.Core/ViewModels/ViewModelBase.cs`: Core ViewModel logic.
- `XP_CALCULATION_GUIDE.md`: Logic for progression and gamification.
- `ToDos.md`: Current development roadmap and upcoming tasks.
