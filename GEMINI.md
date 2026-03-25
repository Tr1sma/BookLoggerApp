# Book Logger App - Gemini AI Context

## Project Overview

**Book Logger App** is a modern, cross-platform mobile application (primarily targeting Android) for managing and tracking reading habits. It features strong gamification elements, including XP, leveling up, and a virtual plant garden that grows as the user reads. 

### Key Technologies
*   **Framework:** .NET 10 MAUI Blazor Hybrid
*   **Language:** C#
*   **UI:** Razor Components (Blazor) & Modern CSS, hosted within a MAUI shell.
*   **Database:** SQLite via Entity Framework (EF) Core 10
*   **Validation:** FluentValidation

### Architecture
The project strictly follows **Clean Architecture** and the **MVVM (Model-View-ViewModel)** pattern:

*   **`BookLoggerApp/` (Presentation Layer):** Contains the MAUI application shell, native platform implementations (Android, Windows, etc.), and the Blazor UI (Razor pages, components, and static web assets in `wwwroot/`).
*   **`BookLoggerApp.Core/` (Domain Layer):** Contains the core business logic, Entities/Models (`Book`, `ReadingSession`, `UserPlant`), ViewModels for the UI, Interfaces (`Services/Abstractions`), and validation logic. Pure C# with no framework dependencies.
*   **`BookLoggerApp.Infrastructure/` (Infrastructure Layer):** Contains the concrete implementations for data access. Includes the EF Core `DbContext`, Code-First Migrations, Repositories, and platform-specific service implementations.
*   **`BookLoggerApp.Tests/` (Test Layer):** Unit and Integration tests using xUnit and FluentAssertions.

## Building and Running

### Prerequisites
*   [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
*   Visual Studio 2022+ or VS Code with C# Dev Kit
*   Android SDK (for building and running the Android target)

### Key Commands

**Restore Dependencies:**
```bash
dotnet restore
```

**Run the Application (Android):**
```bash
dotnet build -f net10.0-android -t:Run
```

**Run Unit Tests:**
```bash
dotnet test
```

**Entity Framework Migrations:**
*   Add a new migration:
    ```bash
    dotnet ef migrations add <MigrationName> --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp
    ```
*   Update the database:
    ```bash
    dotnet ef database update --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp
    ```

## Development Conventions

*   **Offline-First:** The application relies on a local SQLite database. Ensure data access is asynchronous and handles concurrency (the project uses `DbContextFactory` for safe Blazor concurrency).
*   **MVVM Pattern:** UI logic should be strictly separated from presentation. Razor components should bind to properties and commands on their respective `ViewModel` instances (in `BookLoggerApp.Core/ViewModels/`).
*   **Dependency Injection:** Services, Repositories, and ViewModels are injected via MAUI's built-in dependency injection container (configured in `MauiProgram.cs`).
*   **Validation:** Use `FluentValidation` for robust data validation within the Core layer.
*   **Styling:** Mobile-first approach using Modern CSS and custom properties (located in `wwwroot/css/`). The app features a dark mode/cozy theme by default.
