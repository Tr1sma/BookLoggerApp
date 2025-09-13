using BookLoggerApp;
using BookLoggerApp.Core.Services;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using BookLoggerApp.Infrastructure;
using SQLite;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // SQLite connections
        var dbPath = PlatformsDbPath.GetDatabasePath();

        // Register a single SQLiteAsyncConnection for both services
        builder.Services.AddSingleton(new SQLiteAsyncConnection(dbPath));

        // Services
        builder.Services.AddSingleton<IBookService>(sp =>
        {
            var svc = new SqliteBookService(dbPath);
            // Fire-and-forget initialize (for M0 ok); alternativ: await in App start
            svc.InitializeAsync().ConfigureAwait(false);
            return svc;
        });
        builder.Services.AddSingleton<IProgressService>(sp =>
        {
            var conn = sp.GetRequiredService<SQLiteAsyncConnection>();
            return SqliteProgressService.CreateAsync(dbPath).GetAwaiter().GetResult();
        });

        // ViewModels
        builder.Services.AddTransient<BookListViewModel>();
        builder.Services.AddTransient<BookDetailViewModel>();

        return builder.Build();
    }
}
