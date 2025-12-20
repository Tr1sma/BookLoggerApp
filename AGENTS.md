# AGENTS.md

## Build/Test Commands
- Build: `dotnet build BookLoggerApp.sln`
- Run all tests: `dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj`
- Run single test: `dotnet test BookLoggerApp.Tests/BookLoggerApp.Tests.csproj --filter "FullyQualifiedName~YourTestName"`
- EF migrations: `dotnet ef migrations add MigrationName --project BookLoggerApp.Infrastructure --startup-project BookLoggerApp`

## Code Style Guidelines
- **C#/.NET 10** with nullable reference types enabled
- **Formatting**: 4-space indentation, UTF-8, LF line endings, trim trailing whitespace
- **Naming**: PascalCase (classes/methods/properties), camelCase (locals/parameters), interfaces prefixed with 'I'
- **Imports**: System directives first, separate groups, no unnecessary qualifications
- **Documentation**: XML comments on public APIs
- **Async**: Use async/await with CancellationToken parameters
- **ViewModels**: Inherit from ViewModelBase, use `[ObservableProperty]` for CommunityToolkit.Mvvm
- **Validation**: FluentValidation in Core/Validators, custom exceptions in Core/Exceptions
- **Testing**: xUnit + FluentAssertions, arrange-act-assert pattern
- **Architecture**: Layered (Core→Infrastructure→UI), DI with specific lifetimes (services=singleton, repos=transient)
- **EF Core**: SQLite with migrations, RowVersion for concurrency, async operations