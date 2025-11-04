using BookLoggerApp;
using BookLoggerApp.Core.ViewModels;
using BookLoggerApp.Infrastructure;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using Microsoft.EntityFrameworkCore;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        System.Diagnostics.Debug.WriteLine("=== MauiProgram.CreateMauiApp Started ===");

        var builder = MauiApp.CreateBuilder();
        System.Diagnostics.Debug.WriteLine("MauiApp.CreateBuilder completed");

        builder.UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();
        System.Diagnostics.Debug.WriteLine("UseMauiApp<App> and AddMauiBlazorWebView completed");

        // Database path
        var dbPath = PlatformsDbPath.GetDatabasePath();

        // Register EF Core DbContext as Transient (MAUI doesn't have request scopes like ASP.NET)
        builder.Services.AddTransient<AppDbContext>(sp =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
            return new AppDbContext(optionsBuilder.Options);
        });

        // Register Generic Repository
        builder.Services.AddTransient(typeof(IRepository<>), typeof(Repository<>));

        // Register Specific Repositories
        builder.Services.AddTransient<IBookRepository, BookRepository>();
        builder.Services.AddTransient<IReadingSessionRepository, ReadingSessionRepository>();
        builder.Services.AddTransient<IReadingGoalRepository, ReadingGoalRepository>();
        builder.Services.AddTransient<IUserPlantRepository, UserPlantRepository>();

        // Register Services as Transient to match DbContext lifetime
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IBookService, BookLoggerApp.Infrastructure.Services.BookService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IProgressService, BookLoggerApp.Infrastructure.Services.ProgressService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IGenreService, BookLoggerApp.Infrastructure.Services.GenreService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IQuoteService, BookLoggerApp.Infrastructure.Services.QuoteService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IAnnotationService, BookLoggerApp.Infrastructure.Services.AnnotationService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IGoalService, BookLoggerApp.Infrastructure.Services.GoalService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IPlantService, BookLoggerApp.Infrastructure.Services.PlantService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IStatsService, BookLoggerApp.Infrastructure.Services.StatsService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IImageService, BookLoggerApp.Infrastructure.Services.ImageService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IAppSettingsProvider, BookLoggerApp.Infrastructure.Services.AppSettingsProvider>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.IImportExportService, BookLoggerApp.Infrastructure.Services.ImportExportService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.ILookupService, BookLoggerApp.Infrastructure.Services.LookupService>();
        builder.Services.AddTransient<BookLoggerApp.Core.Services.Abstractions.INotificationService, BookLoggerApp.Infrastructure.Services.NotificationService>();

        // ViewModels
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

        System.Diagnostics.Debug.WriteLine("All services registered, building app...");

        var app = builder.Build();

        // Initialize database asynchronously to avoid blocking the UI thread
        System.Diagnostics.Debug.WriteLine("Starting database initialization task...");
        Task.Run(async () =>
        {
            System.Diagnostics.Debug.WriteLine("Database initialization task started");
            try
            {
                using var scope = app.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

//#if DEBUG
//                // In DEBUG mode, delete and recreate the database for a clean start
//                System.Diagnostics.Debug.WriteLine("DEBUG MODE: Deleting existing database...");
//                await dbContext.Database.EnsureDeletedAsync();
//                System.Diagnostics.Debug.WriteLine("Database deleted");
//#endif

                System.Diagnostics.Debug.WriteLine("Checking if database exists...");
                var canConnect = await dbContext.Database.CanConnectAsync();
                System.Diagnostics.Debug.WriteLine($"Can connect to database: {canConnect}");

                System.Diagnostics.Debug.WriteLine("Applying migrations...");
                await dbContext.Database.MigrateAsync();
                System.Diagnostics.Debug.WriteLine("Database migration completed successfully");

                // Verify seed data
                var genreCount = await dbContext.Genres.CountAsync();
                System.Diagnostics.Debug.WriteLine($"Genres in database: {genreCount}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== EXCEPTION IN DATABASE MIGRATION ===");
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
            }
        });

        System.Diagnostics.Debug.WriteLine("=== MauiProgram.CreateMauiApp Completed ===");
        return app;
    }
}
