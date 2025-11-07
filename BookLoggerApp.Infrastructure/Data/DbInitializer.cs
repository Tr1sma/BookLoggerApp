using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Infrastructure;

namespace BookLoggerApp.Infrastructure.Data;

/// <summary>
/// Handles database initialization, migrations, and data fixes.
/// Provides thread-safe initialization with await support via DatabaseInitializationHelper.
/// </summary>
public static class DbInitializer
{
    private static bool _isInitialized = false;
    private static readonly object _lock = new();

    /// <summary>
    /// Initializes the database asynchronously.
    /// This should be called once at application startup.
    /// Notifies DatabaseInitializationHelper when complete.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, ILogger? logger = null)
    {
        lock (_lock)
        {
            if (_isInitialized)
            {
                logger?.LogWarning("Database initialization already completed");
                return;
            }
        }

        try
        {
            logger?.LogInformation("Starting database initialization...");

            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Apply migrations
            await MigrateDatabaseAsync(dbContext, logger);

            // Recalculate user level from TotalXp (fixes corrupted data)
            await RecalculateUserLevelAsync(scope.ServiceProvider, logger);

            // Fix plant image paths
            await FixPlantImagePathsAsync(dbContext, logger);

            // Validate seed data
            await ValidateSeedDataAsync(dbContext, logger);

            lock (_lock)
            {
                _isInitialized = true;
            }

            // Notify Core layer that initialization is complete
            DatabaseInitializationHelper.MarkAsInitialized();
            logger?.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Database initialization failed");
            // Notify Core layer that initialization failed
            DatabaseInitializationHelper.MarkAsFailed(ex);
            throw;
        }
    }

    private static async Task MigrateDatabaseAsync(AppDbContext context, ILogger? logger)
    {
        logger?.LogInformation("Checking database connection...");
        var canConnect = await context.Database.CanConnectAsync();
        logger?.LogInformation("Can connect to database: {CanConnect}", canConnect);

        logger?.LogInformation("Applying migrations...");
        await context.Database.MigrateAsync();
        logger?.LogInformation("Database migrations applied successfully");
    }

    private static async Task RecalculateUserLevelAsync(IServiceProvider services, ILogger? logger)
    {
        logger?.LogInformation("Recalculating user level from TotalXp...");

        var settingsProvider = services.GetService<IAppSettingsProvider>();
        if (settingsProvider is BookLoggerApp.Infrastructure.Services.AppSettingsProvider provider)
        {
            await provider.RecalculateUserLevelAsync();
            logger?.LogInformation("User level recalculation completed");
        }
        else
        {
            logger?.LogWarning("AppSettingsProvider not found or not of expected type");
        }
    }

    private static async Task FixPlantImagePathsAsync(AppDbContext context, ILogger? logger)
    {
        logger?.LogInformation("=== CHECKING PLANT IMAGE PATHS ===");

        var plants = await context.PlantSpecies.ToListAsync();
        logger?.LogInformation("Found {Count} plant species in database", plants.Count);

        bool needsSave = false;

        foreach (var plant in plants)
        {
            logger?.LogDebug("Plant: {Name}, Current ImagePath: '{ImagePath}'", plant.Name, plant.ImagePath);

            if (!string.IsNullOrEmpty(plant.ImagePath))
            {
                string correctPath = plant.ImagePath;

                // Remove leading slash if present
                if (correctPath.StartsWith("/"))
                {
                    correctPath = correctPath.TrimStart('/');
                    logger?.LogDebug("  -> Removed leading slash: {Path}", correctPath);
                }

                // Fix file extension: .png -> .svg
                if (correctPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    correctPath = correctPath[..^4] + ".svg";
                    logger?.LogDebug("  -> Changed extension to .svg: {Path}", correctPath);
                }

                // If it's just a filename, convert to wwwroot path
                if (!correctPath.Contains("/"))
                {
                    correctPath = $"images/plants/{correctPath}";
                    logger?.LogDebug("  -> Added path prefix: {Path}", correctPath);
                }

                if (correctPath != plant.ImagePath)
                {
                    logger?.LogInformation("  -> FINAL UPDATE: '{OldPath}' -> '{NewPath}'",
                        plant.ImagePath, correctPath);
                    plant.ImagePath = correctPath;
                    needsSave = true;
                }
                else
                {
                    logger?.LogDebug("  -> Path is correct, no change needed");
                }
            }
        }

        if (needsSave)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("Plant image paths fixed and saved");
        }
        else
        {
            logger?.LogInformation("All plant image paths are already correct");
        }

        // Verify final paths
        logger?.LogInformation("=== FINAL PLANT IMAGE PATHS ===");
        var finalPlants = await context.PlantSpecies.ToListAsync();
        foreach (var plant in finalPlants)
        {
            logger?.LogDebug("  {Name}: '{ImagePath}'", plant.Name, plant.ImagePath);
        }
    }

    private static async Task ValidateSeedDataAsync(AppDbContext context, ILogger? logger)
    {
        logger?.LogInformation("Validating seed data...");

        var genreCount = await context.Genres.CountAsync();
        logger?.LogInformation("Genres in database: {Count}", genreCount);

        if (genreCount == 0)
        {
            logger?.LogWarning("No genres found in database. Seed data may not have been applied.");
        }

        var plantSpeciesCount = await context.PlantSpecies.CountAsync();
        logger?.LogInformation("Plant species in database: {Count}", plantSpeciesCount);

        if (plantSpeciesCount == 0)
        {
            logger?.LogWarning("No plant species found in database. Seed data may not have been applied.");
        }

        var settingsCount = await context.AppSettings.CountAsync();
        logger?.LogInformation("AppSettings in database: {Count}", settingsCount);

        if (settingsCount == 0)
        {
            logger?.LogWarning("No AppSettings found in database. Seed data may not have been applied.");
        }
    }
}
