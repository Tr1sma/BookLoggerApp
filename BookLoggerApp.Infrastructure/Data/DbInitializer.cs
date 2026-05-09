using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
                DatabaseInitializationHelper.AppendInitLog("InitializeAsync: already initialized, re-signalling gate");
                // Re-signal the gate in case ResetForRetry replaced the TCS after a
                // previous successful init — otherwise Retry() would leave awaiters hanging.
                DatabaseInitializationHelper.MarkAsInitialized();
                return;
            }
        }

        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var scope = services.CreateScope();
        var disposeScope = true;
        MigrationTimings timings = default;
        ICrashReportingService? crashReporting = null;
        AppDbContext? dbContext = null;
        try
        {
            logger?.LogInformation("Starting database initialization...");
            DatabaseInitializationHelper.AppendInitLog("InitializeAsync: starting");

            // Resolve DbContext via the factory so its options include the
            // MigrationLoggingInterceptor (registered only on the factory's options
            // to avoid double-firing per SQL command). The injected AppDbContext
            // service uses a different options instance without the interceptor.
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            dbContext = await contextFactory.CreateDbContextAsync();
            crashReporting = scope.ServiceProvider.GetService<ICrashReportingService>();

            // Critical path: only migrations block the UI. Pages and ViewModels need
            // a usable schema before they can query anything, but every other
            // initialization step below is either idempotent maintenance (seed
            // sync, image path fixes) or creates fallback data that downstream
            // code already handles when absent (e.g. EntitlementStore.GetOrCreateAsync).
            timings = await MigrateDatabaseAsync(dbContext, crashReporting, logger);
            logger?.LogInformation("Migrations finished in {Ms} ms", timings.TotalMs);
            DatabaseInitializationHelper.AppendInitLog(
                $"MigrateDatabaseAsync OK total={timings.TotalMs}ms canConnect={timings.CanConnectMs}ms migrate={timings.MigrateMs}ms schemaDrift={timings.SchemaDriftMs}ms");

            lock (_lock)
            {
                _isInitialized = true;
            }

            // Unblock awaiting ViewModels. Every page can start loading real data now.
            DatabaseInitializationHelper.MarkAsInitialized();
            logger?.LogInformation("UI unblocked after {Ms} ms; running deferred maintenance in background...", totalStopwatch.ElapsedMilliseconds);
            DatabaseInitializationHelper.AppendInitLog(
                $"UI unblocked after {totalStopwatch.ElapsedMilliseconds}ms — running deferred maintenance in background");

            ReportInitSuccess(crashReporting, totalStopwatch.ElapsedMilliseconds, timings, logger);

            // Hand the scope off to the deferred worker and don't dispose it here.
            // The migration DbContext is disposed eagerly — deferred maintenance
            // resolves its own scoped AppDbContext from the captured scope.
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
            DatabaseInitializationHelper.AppendInitLog(
                $"InitializeAsync FAILED after {totalStopwatch.ElapsedMilliseconds}ms — {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException is not null)
            {
                DatabaseInitializationHelper.AppendInitLog(
                    $"  inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
            ReportInitFailure(crashReporting, ex, totalStopwatch.ElapsedMilliseconds, timings, logger);
            DatabaseInitializationHelper.MarkAsFailed(ex);
            throw;
        }
        finally
        {
            if (dbContext is not null)
            {
                await dbContext.DisposeAsync();
            }
            if (disposeScope)
            {
                scope.Dispose();
            }
        }
    }

    private readonly record struct MigrationTimings(long CanConnectMs, long MigrateMs, long SchemaDriftMs)
    {
        public long TotalMs => CanConnectMs + MigrateMs + SchemaDriftMs;
    }

    /// <summary>
    /// Times a few trivial SQL operations to characterize how fast (or slow) the
    /// underlying eMMC/flash storage is responding. On a healthy phone these all
    /// finish in <50ms; on a congested budget Android (Galaxy A16, etc.) they can
    /// run into seconds — which means a multi-statement migration is going to take
    /// minutes through no fault of EF Core. The numbers go straight into the InitLog
    /// so the diagnostic from Settings is self-explanatory.
    /// </summary>
    private static async Task ProbeStorageAsync(AppDbContext context)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await context.Database.ExecuteSqlRawAsync("SELECT 1");
            sw.Stop();
            DatabaseInitializationHelper.AppendInitLog(
                $"  [storage] SELECT 1 = {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            await context.Database.ExecuteSqlRawAsync(
                "CREATE TEMP TABLE _bookheart_probe (x INTEGER); " +
                "INSERT INTO _bookheart_probe VALUES (1); " +
                "DROP TABLE _bookheart_probe;");
            sw.Stop();
            DatabaseInitializationHelper.AppendInitLog(
                $"  [storage] temp write/drop = {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            DatabaseInitializationHelper.AppendInitLog(
                $"  [storage] probe failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets per-connection PRAGMAs that significantly speed up migrations on slow
    /// Android storage without compromising data safety:
    /// - <c>synchronous=NORMAL</c>: with journal_mode=WAL this is the recommended
    ///   setting and survives power loss with at most rolling back the last
    ///   uncommitted transaction.
    /// - <c>temp_store=MEMORY</c>: keeps EF Core's intermediate tables in RAM
    ///   instead of round-tripping through disk.
    /// </summary>
    private static async Task ApplyMigrationPragmasAsync(AppDbContext context)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous = NORMAL;");
            await context.Database.ExecuteSqlRawAsync("PRAGMA temp_store = MEMORY;");
            sw.Stop();
            DatabaseInitializationHelper.AppendInitLog(
                $"  [pragma] synchronous=NORMAL, temp_store=MEMORY ({sw.ElapsedMilliseconds}ms)");
        }
        catch (Exception ex)
        {
            DatabaseInitializationHelper.AppendInitLog(
                $"  [pragma] failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ReportInitSuccess(
        ICrashReportingService? crashReporting,
        long totalMs,
        MigrationTimings timings,
        ILogger? logger)
    {
        if (crashReporting is null) return;
        try
        {
            crashReporting.SetCustomKey("db_init_ms", totalMs.ToString());
            crashReporting.SetCustomKey("db_init_canconnect_ms", timings.CanConnectMs.ToString());
            crashReporting.SetCustomKey("db_init_migrate_ms", timings.MigrateMs.ToString());
            crashReporting.SetCustomKey("db_init_schemadrift_ms", timings.SchemaDriftMs.ToString());
            crashReporting.Log($"db_init ok total={totalMs}ms canConnect={timings.CanConnectMs}ms migrate={timings.MigrateMs}ms schemaDrift={timings.SchemaDriftMs}ms");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to record db_init success telemetry");
        }
    }

    private static void ReportInitFailure(
        ICrashReportingService? crashReporting,
        Exception exception,
        long totalMs,
        MigrationTimings timings,
        ILogger? logger)
    {
        if (crashReporting is null) return;
        try
        {
            var keys = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["source"] = "db_init",
                ["db_init_ms"] = totalMs.ToString(),
                ["db_init_canconnect_ms"] = timings.CanConnectMs.ToString(),
                ["db_init_migrate_ms"] = timings.MigrateMs.ToString(),
                ["db_init_schemadrift_ms"] = timings.SchemaDriftMs.ToString(),
                ["exception_type"] = exception.GetType().FullName ?? "unknown"
            };
            crashReporting.RecordNonFatal(exception, keys);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to record db_init failure telemetry");
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
            DatabaseInitializationHelper.AppendInitLog($"Deferred step '{stepName}' OK ({stepStopwatch.ElapsedMilliseconds}ms)");
        }
        catch (Exception ex)
        {
            stepStopwatch.Stop();
            logger?.LogError(ex, "Deferred step '{Step}' failed after {Ms} ms (non-fatal)", stepName, stepStopwatch.ElapsedMilliseconds);
            DatabaseInitializationHelper.AppendInitLog(
                $"Deferred step '{stepName}' FAILED after {stepStopwatch.ElapsedMilliseconds}ms — {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task<MigrationTimings> MigrateDatabaseAsync(
        AppDbContext context,
        ICrashReportingService? crashReporting,
        ILogger? logger)
    {
        DatabaseInitializationHelper.AppendInitLog("MigrateDatabaseAsync: CanConnectAsync...");
        var canConnectSw = System.Diagnostics.Stopwatch.StartNew();
        logger?.LogInformation("Checking database connection...");
        var canConnect = await context.Database.CanConnectAsync();
        canConnectSw.Stop();
        logger?.LogInformation("Can connect to database: {CanConnect} ({Ms} ms)", canConnect, canConnectSw.ElapsedMilliseconds);
        DatabaseInitializationHelper.AppendInitLog($"  CanConnect={canConnect} ({canConnectSw.ElapsedMilliseconds}ms)");

        DatabaseInitializationHelper.AppendInitLog("MigrateDatabaseAsync: MigrateAsync...");
        var migrateSw = System.Diagnostics.Stopwatch.StartNew();
        logger?.LogInformation("Applying migrations...");

        // Storage characterization — surfaces whether the device's filesystem is the
        // bottleneck. On a healthy phone these are all <50ms. On a Galaxy A16 with
        // congested eMMC they routinely run into the seconds. Knowing this up front
        // lets us interpret slow migrations correctly.
        await ProbeStorageAsync(context);

        // Speed up migrations on slow eMMC. NORMAL is safe with WAL: it still survives
        // power loss without DB corruption (just may roll back the last commit).
        // temp_store=MEMORY keeps EF Core's temp tables out of the SD-card backed FS.
        await ApplyMigrationPragmasAsync(context);

        // Clear any stale __EFMigrationsLock row from a previous app run that crashed
        // or was force-closed mid-migration. EF Core 9's SqliteHistoryRepository polls
        // INSERT OR IGNORE on this table forever if the row is already there; for a
        // single-process Android app any pre-existing row is by definition stale.
        await MigrationRecovery.ClearStaleMigrationLockAsync(context);

        // Apply migrations one at a time so a hung migration is identifiable in the
        // diagnostic log instead of disappearing inside an opaque Database.MigrateAsync()
        // call. 180s per-migration timeout (vs original 60s) accounts for slow eMMC
        // + per-statement fsync overhead — a single migration with ~30 SQL ops can
        // genuinely take 60s+ on budget Android even with NORMAL synchronous mode.
        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
        DatabaseInitializationHelper.AppendInitLog(
            $"  applied={applied.Count} pending={pending.Count}");
        if (applied.Count > 0)
        {
            DatabaseInitializationHelper.AppendInitLog($"  last applied: {applied[^1]}");
        }
        foreach (var p in pending)
        {
            DatabaseInitializationHelper.AppendInitLog($"  pending: {p}");
        }

        if (pending.Count == 0)
        {
            DatabaseInitializationHelper.AppendInitLog("  no pending migrations");
        }
        else
        {
            var migrator = context.Database.GetInfrastructure().GetRequiredService<IMigrator>();
            // Turn on per-SQL logging only for the migration loop. The interceptor
            // appends every command + duration to the InitLog so a hung statement is
            // identifiable down to the exact SQL.
            MigrationLoggingInterceptor.Enabled = true;
            try
            {
                for (int i = 0; i < pending.Count; i++)
                {
                    var name = pending[i];
                    var stepSw = System.Diagnostics.Stopwatch.StartNew();
                    DatabaseInitializationHelper.AppendInitLog(
                        $"  > [{i + 1}/{pending.Count}] {name} ...");
                    try
                    {
                        await migrator.MigrateAsync(name).WaitAsync(TimeSpan.FromSeconds(180));
                        stepSw.Stop();
                        DatabaseInitializationHelper.AppendInitLog(
                            $"  + [{i + 1}/{pending.Count}] {name} OK ({stepSw.ElapsedMilliseconds}ms)");
                    }
                    catch (TimeoutException)
                    {
                        stepSw.Stop();
                        DatabaseInitializationHelper.AppendInitLog(
                            $"  ! [{i + 1}/{pending.Count}] {name} TIMEOUT after {stepSw.ElapsedMilliseconds}ms");
                        throw new TimeoutException(
                            $"Migration '{name}' did not finish within 180 seconds. " +
                            "See Settings → More Info → Diagnostics for the full migration log.");
                    }
                    catch (Exception ex) when (MigrationRecovery.IsSchemaAlreadyAppliedError(ex))
                    {
                        // The migration's ALTER TABLE / CREATE TABLE found that the
                        // schema element already exists. This happens when a previous
                        // run partially applied the schema via SchemaDriftGuard recovery
                        // without updating __EFMigrationsHistory, or when a DB was
                        // upgraded out-of-band. Mark the migration as applied and move
                        // on; SchemaDriftGuard runs after this loop and will fill in
                        // any missing critical columns it knows about.
                        stepSw.Stop();
                        DatabaseInitializationHelper.AppendInitLog(
                            $"  ~ [{i + 1}/{pending.Count}] {name} schema already present " +
                            $"({ex.GetType().Name}: {ex.Message}); recording as applied");
                        await MigrationRecovery.ForceMarkMigrationAppliedAsync(context, name);
                    }
                    catch (Exception ex)
                    {
                        stepSw.Stop();
                        DatabaseInitializationHelper.AppendInitLog(
                            $"  ! [{i + 1}/{pending.Count}] {name} FAILED after {stepSw.ElapsedMilliseconds}ms — {ex.GetType().Name}: {ex.Message}");
                        throw;
                    }
                }
            }
            finally
            {
                MigrationLoggingInterceptor.Enabled = false;
            }
        }

        migrateSw.Stop();
        logger?.LogInformation("Database migrations applied successfully ({Ms} ms)", migrateSw.ElapsedMilliseconds);
        DatabaseInitializationHelper.AppendInitLog(
            $"  MigrateAsync OK ({migrateSw.ElapsedMilliseconds}ms total, {pending.Count} migration(s) applied)");

        // Repair any schema drift where __EFMigrationsHistory claims a migration is applied
        // but the expected columns are missing (observed in the field on V10 upgrades).
        DatabaseInitializationHelper.AppendInitLog("MigrateDatabaseAsync: SchemaDriftGuard...");
        var schemaDriftSw = System.Diagnostics.Stopwatch.StartNew();
        await SchemaDriftGuard.EnsureCriticalColumnsAsync(context, crashReporting, logger);
        schemaDriftSw.Stop();
        logger?.LogInformation("SchemaDriftGuard finished ({Ms} ms)", schemaDriftSw.ElapsedMilliseconds);
        DatabaseInitializationHelper.AppendInitLog($"  SchemaDriftGuard OK ({schemaDriftSw.ElapsedMilliseconds}ms)");

        return new MigrationTimings(
            canConnectSw.ElapsedMilliseconds,
            migrateSw.ElapsedMilliseconds,
            schemaDriftSw.ElapsedMilliseconds);
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
