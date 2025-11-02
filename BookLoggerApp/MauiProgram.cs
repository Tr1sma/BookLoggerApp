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
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

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

        return builder.Build();
    }
}
