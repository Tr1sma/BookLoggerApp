using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Infrastructure.Data.SeedData;

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
    /// Notifies DatabaseInitializationHelper as soon as migrations complete so that
    /// waiting ViewModels can load their data; non-critical maintenance (plant and
    /// decoration sync, XP recalc, entitlement row, image path fixes, seed validation)
    /// runs afterwards in the background without blocking the UI.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, ILogger? logger = null)
    {
        lock (_lock)
        {
            if (_isInitialized)
            {
                logger?.LogWarning("Database initialization already completed");
                // Re-signal the gate in case ResetForRetry replaced the TCS after a
                // previous successful init — otherwise Retry() would leave awaiters hanging.
                DatabaseInitializationHelper.MarkAsInitialized();
                return;
            }
        }

        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var scope = services.CreateScope();
        var disposeScope = true;
        try
        {
            logger?.LogInformation("Starting database initialization...");

            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var crashReporting = scope.ServiceProvider.GetService<ICrashReportingService>();

            // Critical path: only migrations block the UI. Pages and ViewModels need
            // a usable schema before they can query anything, but every other
            // initialization step below is either idempotent maintenance (seed
            // sync, image path fixes) or creates fallback data that downstream
            // code already handles when absent (e.g. EntitlementStore.GetOrCreateAsync).
            var migrateStopwatch = System.Diagnostics.Stopwatch.StartNew();
            await MigrateDatabaseAsync(dbContext, crashReporting, logger);
            migrateStopwatch.Stop();
            logger?.LogInformation("Migrations finished in {Ms} ms", migrateStopwatch.ElapsedMilliseconds);

            lock (_lock)
            {
                _isInitialized = true;
            }

            // Unblock awaiting ViewModels. Every page can start loading real data now.
            DatabaseInitializationHelper.MarkAsInitialized();
            logger?.LogInformation("UI unblocked after {Ms} ms; running deferred maintenance in background...", totalStopwatch.ElapsedMilliseconds);

            // Hand the scope off to the deferred worker and don't dispose it here.
            disposeScope = false;
            var capturedScope = scope;
            _ = Task.Run(async () =>
            {
                try
                {
                    await RunDeferredMaintenanceAsync(capturedScope, totalStopwatch, logger);
                }
                finally
                {
                    capturedScope.Dispose();
                }
            });
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Database initialization failed");
            DatabaseInitializationHelper.MarkAsFailed(ex);
            throw;
        }
        finally
        {
            if (disposeScope)
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Non-critical work that used to block app startup. Each step runs inside its
    /// own try/catch so a single failure does not skip the rest, and exceptions are
    /// logged rather than rethrown — the app is already usable at this point.
    /// </summary>
    private static async Task RunDeferredMaintenanceAsync(
        IServiceScope scope,
        System.Diagnostics.Stopwatch totalStopwatch,
        ILogger? logger)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await RunDeferredStepAsync("RecalculateUserLevel", logger,
            () => RecalculateUserLevelAsync(scope.ServiceProvider, logger));
        await RunDeferredStepAsync("FixPlantImagePaths", logger,
            () => FixPlantImagePathsAsync(dbContext, logger));
        await RunDeferredStepAsync("EnsurePlantDataSynced", logger,
            () => EnsurePlantDataSyncedAsync(dbContext, logger));
        await RunDeferredStepAsync("EnsureDecorationDataSynced", logger,
            () => EnsureDecorationDataSyncedAsync(dbContext, logger));
        await RunDeferredStepAsync("EnsureUserEntitlement", logger,
            () => EnsureUserEntitlementAsync(dbContext, logger));
        await RunDeferredStepAsync("ValidateSeedData", logger,
            () => ValidateSeedDataAsync(dbContext, logger));

        totalStopwatch.Stop();
        logger?.LogInformation("Database initialization fully completed in {Ms} ms", totalStopwatch.ElapsedMilliseconds);
    }

    private static async Task RunDeferredStepAsync(string stepName, ILogger? logger, Func<Task> step)
    {
        var stepStopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await step();
            stepStopwatch.Stop();
            logger?.LogInformation("Deferred step '{Step}' completed in {Ms} ms", stepName, stepStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stepStopwatch.Stop();
            logger?.LogError(ex, "Deferred step '{Step}' failed after {Ms} ms (non-fatal)", stepName, stepStopwatch.ElapsedMilliseconds);
        }
    }

    private static async Task MigrateDatabaseAsync(
        AppDbContext context,
        ICrashReportingService? crashReporting,
        ILogger? logger)
    {
        logger?.LogInformation("Checking database connection...");
        var canConnect = await context.Database.CanConnectAsync();
        logger?.LogInformation("Can connect to database: {CanConnect}", canConnect);

        logger?.LogInformation("Applying migrations...");
        await context.Database.MigrateAsync();
        logger?.LogInformation("Database migrations applied successfully");

        // Repair any schema drift where __EFMigrationsHistory claims a migration is applied
        // but the expected columns are missing (observed in the field on V10 upgrades).
        await SchemaDriftGuard.EnsureCriticalColumnsAsync(context, crashReporting, logger);
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


    private static async Task EnsureDecorationDataSyncedAsync(AppDbContext context, ILogger? logger)
    {
        logger?.LogInformation("=== SYNCING DECORATION DATA ===");

        var definedDecorations = DecorationSeedData.GetDecorations().ToList();
        var existingDecorations = await context.ShopItems
            .Where(si => si.ItemType == BookLoggerApp.Core.Enums.ShopItemType.Decoration)
            .ToDictionaryAsync(si => si.Id);

        bool hasChanges = false;

        foreach (var def in definedDecorations)
        {
            if (existingDecorations.TryGetValue(def.Id, out var existing))
            {
                if (existing.Name != def.Name ||
                    existing.Description != def.Description ||
                    existing.Cost != def.Cost ||
                    existing.ImagePath != def.ImagePath ||
                    existing.UnlockLevel != def.UnlockLevel ||
                    existing.IsAvailable != def.IsAvailable ||
                    existing.SlotWidth != def.SlotWidth ||
                    existing.SpecialAbilityKey != def.SpecialAbilityKey ||
                    existing.IsSingleton != def.IsSingleton ||
                    existing.IsFreeTier != def.IsFreeTier ||
                    existing.IsUltimateTier != def.IsUltimateTier)
                {
                    logger?.LogInformation("Updating decoration '{Name}'...", def.Name);

                    existing.Name = def.Name;
                    existing.Description = def.Description;
                    existing.Cost = def.Cost;
                    existing.ImagePath = def.ImagePath;
                    existing.UnlockLevel = def.UnlockLevel;
                    existing.IsAvailable = def.IsAvailable;
                    existing.SlotWidth = def.SlotWidth;
                    existing.SpecialAbilityKey = def.SpecialAbilityKey;
                    existing.IsSingleton = def.IsSingleton;
                    existing.IsFreeTier = def.IsFreeTier;
                    existing.IsUltimateTier = def.IsUltimateTier;

                    hasChanges = true;
                }
            }
            else
            {
                logger?.LogInformation("Adding missing decoration '{Name}'...", def.Name);
                context.ShopItems.Add(def);
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("Decoration data synced successfully.");
        }
        else
        {
            logger?.LogInformation("Decoration data is already up to date.");
        }
    }

    private static async Task EnsurePlantDataSyncedAsync(AppDbContext context, ILogger? logger)
    {
        logger?.LogInformation("=== SYNCING PLANT DATA (PROD FIX) ===");
        
        var definedPlants = PlantSeedData.GetPlants().ToList();
        var existingPlants = await context.PlantSpecies.ToDictionaryAsync(p => p.Id);
        
        bool hasChanges = false;
        
        foreach (var def in definedPlants)
        {
            if (existingPlants.TryGetValue(def.Id, out var existing))
            {
                // Update properties if changed
                if (existing.UnlockLevel != def.UnlockLevel ||
                    existing.BaseCost != def.BaseCost ||
                    existing.GrowthRate != def.GrowthRate ||
                    existing.XpBoostPercentage != def.XpBoostPercentage ||
                    existing.MaxLevel != def.MaxLevel ||
                    existing.WaterIntervalDays != def.WaterIntervalDays ||
                    existing.ImagePath != def.ImagePath ||
                    existing.Name != def.Name ||
                    existing.Description != def.Description ||
                    existing.IsAvailable != def.IsAvailable ||
                    existing.SpecialAbilityKey != def.SpecialAbilityKey ||
                    existing.IsFreeTier != def.IsFreeTier ||
                    existing.IsPrestigeTier != def.IsPrestigeTier)
                {
                    logger?.LogInformation("Updating plant '{Name}' stats...", def.Name);

                    existing.UnlockLevel = def.UnlockLevel;
                    existing.BaseCost = def.BaseCost;
                    existing.GrowthRate = def.GrowthRate;
                    existing.XpBoostPercentage = def.XpBoostPercentage;
                    existing.MaxLevel = def.MaxLevel;
                    existing.WaterIntervalDays = def.WaterIntervalDays;
                    existing.ImagePath = def.ImagePath;
                    existing.Name = def.Name;
                    existing.Description = def.Description;
                    existing.IsAvailable = def.IsAvailable;
                    existing.SpecialAbilityKey = def.SpecialAbilityKey;
                    existing.IsFreeTier = def.IsFreeTier;
                    existing.IsPrestigeTier = def.IsPrestigeTier;

                    hasChanges = true;
                }
            }
            else
            {
                logger?.LogInformation("Adding missing plant '{Name}'...", def.Name);
                context.PlantSpecies.Add(def);
                hasChanges = true;
            }
        }
        
        if (hasChanges)
        {
            await context.SaveChangesAsync();
            logger?.LogInformation("Plant data synced successfully.");
        }
        else
        {
            logger?.LogInformation("Plant data is already up to date.");
        }
    }

    private static async Task EnsureUserEntitlementAsync(AppDbContext context, ILogger? logger)
    {
        logger?.LogInformation("=== ENSURING USER ENTITLEMENT ROW ===");

        bool hasAny = await context.UserEntitlements.AnyAsync();
        if (hasAny)
        {
            logger?.LogInformation("UserEntitlement row already present; no action needed.");
            return;
        }

        context.UserEntitlements.Add(new Core.Models.UserEntitlement
        {
            Id = Guid.NewGuid(),
            Tier = Core.Entitlements.SubscriptionTier.Free,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        logger?.LogInformation("Default Free UserEntitlement row created.");
    }
}
