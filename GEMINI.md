# Project Overview

**Book Logger App** is a .NET 10 MAUI Blazor Hybrid application designed for managing and tracking book collections with gamification elements. It allows users to log books, track reading sessions, manage reading goals, and earn virtual rewards (XP, plants) to encourage reading habits.

The application follows a **Clean Architecture** approach with **MVVM (Model-View-ViewModel)** for the presentation layer. It leverages **SQLite** for local data storage and **Entity Framework Core 10** for data access.

# Building and Running

## Prerequisites
*   .NET 10 SDK
*   Visual Studio 2022+ or VS Code with C# Dev Kit
*   Android SDK (for Android deployment)

## Build Commands

```bash
# Restore dependencies
dotnet restore

# Build the entire solution
dotnet build BookLoggerApp.sln

# Build for specific platform (e.g., Android)
dotnet build -f net10.0-android -t:Run
```

## Running Tests

The project uses **xUnit** for unit testing and **FluentAssertions** for assertions.

```bash
# Run all tests
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj

# Run specific tests
dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~YourTestName"
```

## Database Migrations (EF Core)

Database changes are managed via EF Core migrations. Commands should be run from the solution root.

```bash
# Add a new migration
dotnet ef migrations add <MigrationName> --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp

# Update database
dotnet ef database update --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp
```

# Architecture & Tech Stack

## Layers
1.  **BookLoggerApp (Presentation):**
    *   Contains the UI (Blazor Components, Razor Pages).
    *   `MauiProgram.cs`: Dependency Injection configuration, Service registration, Exception handling.
    *   `wwwroot`: Static assets (CSS, images).
    *   Platform-specific code in `Platforms/`.
2.  **BookLoggerApp.Core (Domain):**
    *   **Models:** Core entities (Book, ReadingSession, etc.).
    *   **Services/Abstractions:** Interfaces for business logic.
    *   **ViewModels:** MVVM logic for the UI.
    *   **Validators:** FluentValidation rules.
    *   **Exceptions:** Custom domain exceptions.
3.  **BookLoggerApp.Infrastructure:**
    *   **Data:** EF Core `AppDbContext`, Database Initialization.
    *   **Repositories:** Implementation of data access patterns.
    *   **Services:** Implementation of domain service interfaces.

## Key Patterns
*   **MAUI Blazor Hybrid:** Combines native device capabilities with web-based UI (Blazor).
*   **MVVM:** Separation of concerns between UI (Razor) and logic (ViewModels).
*   **Repository & Unit of Work:**  Encapsulates data access; `IUnitOfWork` ensures transaction consistency.
*   **Dependency Injection:** All services, ViewModels, and validators are registered in the DI container.
*   **Result Pattern:** (Implicit via exceptions/flow) or explicit return types for operations.

# Development Conventions

*   **Service Registration:** Add new services to `RegisterBusinessServices` in `MauiProgram.cs`.
*   **ViewModels:** Registered as `Transient` in `MauiProgram.cs`.
*   **Database Access:** NEVER inject `AppDbContext` or Repositories directly into UI/ViewModels. Use `IUnitOfWork` or Service abstractions.
*   **Async/Await:** Use async methods for all I/O operations (database, file system).
*   **Validation:** Use `FluentValidation` validators located in `BookLoggerApp.Core/Validators`.
*   **Error Handling:** Custom exceptions (e.g., `EntityNotFoundException`) are handled by the global exception handler or specific service logic.

# Key Files
*   `BookLoggerApp/MauiProgram.cs`: Application entry point and DI configuration.
*   `BookLoggerApp.Infrastructure/Data/AppDbContext.cs`: EF Core database context.
*   `BookLoggerApp.Core/ViewModels/`: Logic for individual screens/features.
*   `BookLoggerApp.Infrastructure/Services/`: Business logic implementations.
