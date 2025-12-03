using BookLoggerApp;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.ViewModels;
using BookLoggerApp.Infrastructure;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        System.Diagnostics.Debug.WriteLine("=== MauiProgram.CreateMauiApp Started ===");

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();

        // Configure platform-specific handlers
        ConfigurePlatformHandlers(builder);

        ConfigureLogging(builder);
        RegisterDatabase(builder);
        RegisterRepositories(builder);
        RegisterBusinessServices(builder);
        RegisterViewModels(builder);
        RegisterValidators(builder);

        System.Diagnostics.Debug.WriteLine("All services registered, building app...");

        var app = builder.Build();

        // Setup global exception handler
        SetupGlobalExceptionHandler(app);

        InitializeDatabase(app);

        System.Diagnostics.Debug.WriteLine("=== MauiProgram.CreateMauiApp Completed ===");
        return app;
    }

    private static void ConfigurePlatformHandlers(MauiAppBuilder builder)
    {
#if ANDROID
        // Configure BlazorWebView to use custom WebChromeClient for camera permissions
        Microsoft.AspNetCore.Components.WebView.Maui.BlazorWebViewHandler.BlazorWebViewMapper.AppendToMapping(
            "CustomWebChromeClient",
            (handler, view) =>
            {
                if (handler.PlatformView is Android.Webkit.WebView webView)
                {
                    webView.SetWebChromeClient(new BookLoggerApp.Platforms.Android.CustomWebChromeClient());
                    System.Diagnostics.Debug.WriteLine("CustomWebChromeClient attached to BlazorWebView");
                }
            });
#endif
    }

    private static void ConfigureLogging(MauiAppBuilder builder)
    {
#if DEBUG
        builder.Logging.AddDebug();
#endif
        // Add Memory Cache for performance optimization
        builder.Services.AddMemoryCache();
    }

    private static void RegisterDatabase(MauiAppBuilder builder)
    {
        var dbPath = PlatformsDbPath.GetDatabasePath();

        // Register DbContextFactory for creating DbContext instances on demand
        // This is the recommended approach for Blazor to avoid concurrency issues
        builder.Services.AddDbContextFactory<AppDbContext>(options =>
        {
            options.UseSqlite($"Data Source={dbPath}");
        });

        // Also register DbContext as Transient for compatibility with existing code
        // Each injection gets a fresh instance from the factory
        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite($"Data Source={dbPath}");
        }, ServiceLifetime.Transient);
    }

    private static void RegisterRepositories(MauiAppBuilder builder)
    {
        // Register Unit of Work as Transient
        // UnitOfWork creates all repositories internally with the same DbContext instance
        // This ensures all repositories in a single operation share the same context
        builder.Services.AddTransient<IUnitOfWork, UnitOfWork>();

        // NOTE: Individual repositories are no longer registered here
        // All services should use IUnitOfWork instead of direct repository injection
    }

    private static void RegisterBusinessServices(MauiAppBuilder builder)
    {
        // Register File System Abstraction as Singleton (infrastructure layer)
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IFileSystem, BookLoggerApp.Infrastructure.Services.FileSystemAdapter>();

        // Register Services as Singleton (shares UnitOfWork and DbContext for transaction consistency)
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IBookService, BookLoggerApp.Infrastructure.Services.BookService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IProgressService, BookLoggerApp.Infrastructure.Services.ProgressService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IGenreService, BookLoggerApp.Infrastructure.Services.GenreService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IQuoteService, BookLoggerApp.Infrastructure.Services.QuoteService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IAnnotationService, BookLoggerApp.Infrastructure.Services.AnnotationService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IGoalService, BookLoggerApp.Infrastructure.Services.GoalService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IPlantService, BookLoggerApp.Infrastructure.Services.PlantService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IStatsService, BookLoggerApp.Infrastructure.Services.StatsService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IImageService, BookLoggerApp.Infrastructure.Services.ImageService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IAppSettingsProvider, BookLoggerApp.Infrastructure.Services.AppSettingsProvider>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IProgressionService, BookLoggerApp.Infrastructure.Services.ProgressionService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.IImportExportService, BookLoggerApp.Infrastructure.Services.ImportExportService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.ILookupService, BookLoggerApp.Infrastructure.Services.LookupService>();
        builder.Services.AddSingleton<BookLoggerApp.Core.Services.Abstractions.INotificationService, BookLoggerApp.Infrastructure.Services.NotificationService>();

        // Register MAUI-specific services
        builder.Services.AddSingleton<BookLoggerApp.Services.IPermissionService, BookLoggerApp.Services.PermissionService>();
    }

    private static void RegisterViewModels(MauiAppBuilder builder)
    {
        builder.Services.AddTransient<BookListViewModel>();
        builder.Services.AddTransient<BookDetailViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<BookshelfViewModel>();
        builder.Services.AddTransient<BookEditViewModel>();
        builder.Services.AddTransient<ReadingViewModel>();
        builder.Services.AddTransient<GoalsViewModel>();
        builder.Services.AddTransient<StatsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<PlantShopViewModel>();
        builder.Services.AddTransient<UserProgressViewModel>();
    }

    private static void RegisterValidators(MauiAppBuilder builder)
    {
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IValidationService, BookLoggerApp.Infrastructure.Services.ValidationService>();
        builder.Services.AddTransient<FluentValidation.IValidator<BookLoggerApp.Core.Models.Book>, BookLoggerApp.Core.Validators.BookValidator>();
        builder.Services.AddTransient<FluentValidation.IValidator<BookLoggerApp.Core.Models.ReadingSession>, BookLoggerApp.Core.Validators.ReadingSessionValidator>();
        builder.Services.AddTransient<FluentValidation.IValidator<BookLoggerApp.Core.Models.ReadingGoal>, BookLoggerApp.Core.Validators.ReadingGoalValidator>();
        builder.Services.AddTransient<FluentValidation.IValidator<BookLoggerApp.Core.Models.UserPlant>, BookLoggerApp.Core.Validators.UserPlantValidator>();
    }

    private static void SetupGlobalExceptionHandler(MauiApp app)
    {
        // Global exception handler for unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var exception = (Exception)args.ExceptionObject;
            var logger = app.Services.GetService<ILogger<MauiApp>>();

            logger?.LogCritical(exception, "Unhandled exception occurred");

            // Log user-friendly message for custom exceptions
            if (exception is BookLoggerException bookLoggerEx)
            {
                logger?.LogError("Application error: {Message}", bookLoggerEx.Message);

                // Specific handling for different exception types
                switch (bookLoggerEx)
                {
                    case EntityNotFoundException notFoundEx:
                        logger?.LogWarning("Entity not found: {EntityType} with ID {EntityId}",
                            notFoundEx.EntityType.Name, notFoundEx.EntityId);
                        break;

                    case ConcurrencyException concurrencyEx:
                        logger?.LogWarning("Concurrency conflict: {Message}", concurrencyEx.Message);
                        break;

                    case InsufficientFundsException fundsEx:
                        logger?.LogWarning("Insufficient funds: Required {Required}, Available {Available}",
                            fundsEx.Required, fundsEx.Available);
                        break;

                    case ValidationException validationEx:
                        logger?.LogWarning("Validation failed: {Errors}", string.Join(", ", validationEx.Errors));
                        break;
                }
            }

            System.Diagnostics.Debug.WriteLine($"=== UNHANDLED EXCEPTION ===");
            System.Diagnostics.Debug.WriteLine($"Type: {exception.GetType().FullName}");
            System.Diagnostics.Debug.WriteLine($"Message: {exception.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack: {exception.StackTrace}");
            System.Diagnostics.Debug.WriteLine("=========================");
        };

        // MAUI-specific unhandled exception handler
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            var logger = app.Services.GetService<ILogger<MauiApp>>();
            logger?.LogError(args.Exception, "Unobserved task exception");

            System.Diagnostics.Debug.WriteLine($"=== UNOBSERVED TASK EXCEPTION ===");
            System.Diagnostics.Debug.WriteLine($"Exception: {args.Exception}");
            System.Diagnostics.Debug.WriteLine("=================================");

            // Mark as observed to prevent app crash
            args.SetObserved();
        };
    }

    private static void InitializeDatabase(MauiApp app)
    {
        // Initialize database using DbInitializer
        // This runs asynchronously but provides EnsureInitializedAsync() for ViewModels to await
        System.Diagnostics.Debug.WriteLine("Starting database initialization...");
        _ = Task.Run(async () =>
        {
            try
            {
                var logger = app.Services.GetService<ILogger<AppDbContext>>();
                await DbInitializer.InitializeAsync(app.Services, logger);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== EXCEPTION IN DATABASE INITIALIZATION ===");
                System.Diagnostics.Debug.WriteLine($"Exception Type: {ex.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.GetType().FullName}");
                    System.Diagnostics.Debug.WriteLine($"Inner Message: {ex.InnerException.Message}");
                    System.Diagnostics.Debug.WriteLine($"Inner Stack: {ex.InnerException.StackTrace}");
                }
                System.Diagnostics.Debug.WriteLine("=== END EXCEPTION ===");
                throw; // Re-throw to ensure TCS gets the exception
            }
        });
    }
}
