# Project Overview

This is a .NET MAUI Blazor Hybrid application for logging books. It allows users to manage and track their books, save reading progress, and view statistics. The application is built with .NET 9 and uses SQLite for local data storage. The architecture follows the Model-View-ViewModel (MVVM) pattern.

# Building and Running

To build and run this project, you will need to have the .NET MAUI workload installed. You can then build and run the application using the following commands:

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the project on a specific platform (e.g., android)
dotnet build -t:Run -f net9.0-android
```

# Development Conventions

*   **MVVM:** The project follows the MVVM pattern. Views are defined in `.razor` files, and ViewModels are in the `BookLoggerApp.Core/ViewModels` directory.
*   **Dependency Injection:** Services and ViewModels are registered in `MauiProgram.cs` and injected into their dependencies.
*   **Services:** Business logic is encapsulated in services found in the `BookLoggerApp.Core/Services` directory.
*   **Testing:** Unit tests are located in the `BookLoggerApp.Tests` project. The project uses xUnit for testing and FluentAssertions for assertions. To run the tests, use the following command:

```bash
dotnet test
```