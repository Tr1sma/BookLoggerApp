using System.Globalization;
using System.Text.Json;
using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Compression;

namespace BookLoggerApp.Infrastructure.Services;

/// <summary>Uses DbContextFactory for thread-safe operations.</summary>
public class ImportExportService : IImportExportService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<ImportExportService>? _logger;
    private readonly IFileSystem _fileSystem;
    private readonly IAppSettingsProvider _appSettingsProvider;
    private readonly string _backupDirectory;
    private readonly string _basePath;

    // Zip Bomb protection
    private const long MaxTotalExtractionSize = 1024L * 1024L * 1024L; // 1 GB
    private const int MaxEntryCount = 10000;

    public ImportExportService(
        IDbContextFactory<AppDbContext> contextFactory,
        IFileSystem fileSystem,
        IAppSettingsProvider appSettingsProvider,
        ILogger<ImportExportService>? logger = null,
        string? appDataPath = null)
    {
        _contextFactory = contextFactory;
        _fileSystem = fileSystem;
        _appSettingsProvider = appSettingsProvider;
        _logger = logger;

        _basePath = appDataPath ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _backupDirectory = _fileSystem.CombinePath(_basePath, "backups");
        _fileSystem.CreateDirectory(_backupDirectory);
    }

    public async Task<string> ExportToJsonAsync(CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Starting JSON export");

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var books = await context.Books
                .Include(b => b.BookGenres)
                    .ThenInclude(bg => bg.Genre)
                .Include(b => b.ReadingSessions)
                .Include(b => b.Quotes)
                .Include(b => b.Annotations)
                .Include(b => b.WishlistInfo)
                .ToListAsync(ct);

            var goals = await context.ReadingGoals.ToListAsync(ct);
            var plants = await context.UserPlants
                .Include(p => p.Species)
                .ToListAsync(ct);
            var settings = await context.AppSettings.FirstOrDefaultAsync(ct);

            var exportData = new
            {
                ExportDate = DateTime.UtcNow,
                Version = "1.0",
                Books = books,
                ReadingGoals = goals,
                UserPlants = plants,
                Settings = settings
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            };

            var json = JsonSerializer.Serialize(exportData, options);

            _logger?.LogInformation("JSON export completed. Books: {Count}", books.Count);

            return json;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to export to JSON");
            throw;
        }
    }

    public async Task<string> ExportToCsvAsync(CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Starting CSV export");

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var books = await context.Books
                .Include(b => b.BookGenres)
                    .ThenInclude(bg => bg.Genre)
                .ToListAsync(ct);

            using var writer = new StringWriter();
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            });

            var flatBooks = books.Select(b => new
            {
                b.Id,
                b.Title,
                b.Author,
                b.ISBN,
                b.Publisher,
                b.PublicationYear,
                b.Language,
                b.Description,
                b.PageCount,
                b.CurrentPage,
                b.CoverImagePath,
                Status = b.Status.ToString(),
                Rating = b.AverageRating,
                b.DateAdded,
                b.DateStarted,
                b.DateCompleted,
                Genres = string.Join(";", b.BookGenres.Where(bg => bg.Genre != null).Select(bg => bg.Genre.Name))
            });

            await csv.WriteRecordsAsync(flatBooks, ct);
            await csv.FlushAsync();

            var csvContent = writer.ToString();

            _logger?.LogInformation("CSV export completed. Books: {Count}", books.Count);

            return csvContent;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to export to CSV");
            throw;
        }
    }

    public async Task<int> ImportFromJsonAsync(string json, CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Starting JSON import");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("books", out var booksElement))
            {
                throw new InvalidOperationException("Invalid JSON format: 'books' property not found");
            }

            var books = JsonSerializer.Deserialize<List<Book>>(booksElement.GetRawText(), options);

            if (books == null || books.Count == 0)
            {
                _logger?.LogWarning("No books found in JSON import");
                return 0;
            }

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            int importedCount = 0;
            foreach (var book in books)
            {
                var existingBook = await context.Books
                    .FirstOrDefaultAsync(b =>
                        (b.ISBN != null && b.ISBN == book.ISBN) ||
                        (b.Title == book.Title && b.Author == book.Author), ct);

                if (existingBook == null)
                {
                    context.Books.Add(book);
                    importedCount++;
                }
                else
                {
                    _logger?.LogInformation("Book already exists, skipping: {Title} by {Author}",
                        book.Title, book.Author);
                }
            }

            await context.SaveChangesAsync(ct);

            _logger?.LogInformation("JSON import completed. Imported: {Count}", importedCount);

            return importedCount;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to import from JSON");
            throw;
        }
    }

    public async Task<int> ImportFromCsvAsync(string csv, CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Starting CSV import");

            using var reader = new StringReader(csv);
            using var csvReader = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null // Ignore missing fields
            });

            var records = csvReader.GetRecords<BookCsvRecord>().ToList();

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Pre-load genres for find-or-create
            var existingGenres = await context.Genres.ToListAsync(ct);
            var genreLookup = existingGenres.ToDictionary(g => g.Name, StringComparer.OrdinalIgnoreCase);

            int importedCount = 0;
            foreach (var record in records)
            {
                var existingBook = await context.Books
                    .FirstOrDefaultAsync(b =>
                        (b.ISBN != null && b.ISBN == record.ISBN) ||
                        (b.Title == record.Title && b.Author == record.Author), ct);

                if (existingBook == null)
                {
                    var book = new Book
                    {
                        Title = record.Title,
                        Author = record.Author,
                        ISBN = record.ISBN,
                        Publisher = record.Publisher,
                        PublicationYear = record.PublicationYear,
                        Language = record.Language,
                        Description = record.Description,
                        PageCount = record.PageCount,
                        CurrentPage = record.CurrentPage,
                        CoverImagePath = record.CoverImagePath,
                        Status = Enum.TryParse<ReadingStatus>(record.Status, out var status) ? status : ReadingStatus.Planned,

                        DateAdded = record.DateAdded ?? DateTime.UtcNow,
                        DateStarted = record.DateStarted,
                        DateCompleted = record.DateCompleted
                    };

                    context.Books.Add(book);

                    if (!string.IsNullOrWhiteSpace(record.Genres))
                    {
                        var genreNames = record.Genres.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var genreName in genreNames)
                        {
                            if (!genreLookup.TryGetValue(genreName, out var genre))
                            {
                                genre = new Genre { Name = genreName };
                                context.Genres.Add(genre);
                                genreLookup[genreName] = genre;
                            }

                            context.BookGenres.Add(new BookGenre
                            {
                                BookId = book.Id,
                                GenreId = genre.Id
                            });
                        }
                    }

                    importedCount++;
                }
                else
                {
                    _logger?.LogInformation("Book already exists, skipping: {Title} by {Author}",
                        record.Title, record.Author);
                }
            }

            await context.SaveChangesAsync(ct);

            _logger?.LogInformation("CSV import completed. Imported: {Count}", importedCount);

            return importedCount;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to import from CSV");
            throw;
        }
    }

    public async Task<string> CreateBackupAsync(CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Creating backup (ZIP format)");

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var connectionString = context.Database.GetConnectionString();
            var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
            var dbPath = builder.DataSource;

            if (string.IsNullOrWhiteSpace(dbPath) || !_fileSystem.FileExists(dbPath))
            {
                throw new InvalidOperationException("Database file not found");
            }

            var tempBackupDir = _fileSystem.CombinePath(_backupDirectory, $"temp_{Guid.NewGuid()}");
            _fileSystem.CreateDirectory(tempBackupDir);

            try
            {
                var destDbPath = _fileSystem.CombinePath(tempBackupDir, "booklogger.db");

                try { await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);", ct); } catch { }

                _fileSystem.CopyFile(dbPath, destDbPath, overwrite: true);

                var coversSourceDir = _fileSystem.CombinePath(_basePath, "covers");
                var coversDestDir = _fileSystem.CombinePath(tempBackupDir, "covers");

                if (_fileSystem.DirectoryExists(coversSourceDir))
                {
                    _fileSystem.CreateDirectory(coversDestDir);
                    CopyDirectory(coversSourceDir, coversDestDir);
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupZipName = $"bookheart_backup_{timestamp}.zip";
                var backupZipPath = _fileSystem.CombinePath(_backupDirectory, backupZipName);

                if (File.Exists(backupZipPath)) File.Delete(backupZipPath);

                ZipFile.CreateFromDirectory(tempBackupDir, backupZipPath);

                _logger?.LogInformation("Backup created at: {Path}", backupZipPath);

                var settings = await context.AppSettings.FirstOrDefaultAsync(ct);
                if (settings != null)
                {
                    settings.LastBackupDate = DateTime.UtcNow;
                    await context.SaveChangesAsync(ct);
                }

                return backupZipPath;
            }
            finally
            {
                if (Directory.Exists(tempBackupDir))
                {
                    Directory.Delete(tempBackupDir, true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create backup");
            throw;
        }
    }

    private void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) return;

        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (DirectoryInfo subDir in dir.GetDirectories())
        {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }

    public async Task RestoreFromBackupAsync(string backupPath, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Restoring from backup: {Path}", backupPath);
            progress?.Report($"Starting restore from {Path.GetFileName(backupPath)}");

            // Gate on DbInitializer so we never swap the DB file out from under an
            // in-flight scoped DbContext. Defense in depth: the ViewModel is expected
            // to have awaited this already, but direct callers get the same guarantee.
            progress?.Report("Waiting for database initialization to complete");
            await BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.EnsureInitializedAsync();
            progress?.Report("Database initialization confirmed");

            if (!File.Exists(backupPath))
            {
                throw new FileNotFoundException("Backup file not found", backupPath);
            }

            var tempExtractDir = _fileSystem.CombinePath(_backupDirectory, $"restore_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempExtractDir);

            try
            {
                progress?.Report("Extracting ZIP archive");
                // Manual extraction with path validation: prevents Zip Slip and Zip Bomb
                using (var archive = ZipFile.OpenRead(backupPath))
                {
                    long totalSize = 0;
                    int entryCount = 0;

                    foreach (var entry in archive.Entries)
                    {
                        entryCount++;
                        if (entryCount > MaxEntryCount)
                        {
                            throw new IOException("Zip Bomb detected: Too many entries in archive.");
                        }

                        totalSize += entry.Length;
                        if (totalSize > MaxTotalExtractionSize)
                        {
                            throw new IOException("Zip Bomb detected: Total extraction size exceeds limit.");
                        }

                        var destinationPath = Path.GetFullPath(Path.Combine(tempExtractDir, entry.FullName));
                        var tempDirFullPath = Path.GetFullPath(tempExtractDir);

                        if (!tempDirFullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                        {
                            tempDirFullPath += Path.DirectorySeparatorChar;
                        }

                        // Zip Slip check
                        if (!destinationPath.StartsWith(tempDirFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new IOException($"Zip Slip vulnerability detected: {entry.FullName}");
                        }

                        if (entry.Name == "")
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                            entry.ExtractToFile(destinationPath, overwrite: true);
                        }
                    }
                }

                // Case-insensitive search for database file
                var extractedDbPath = Directory
                    .EnumerateFiles(tempExtractDir, "*", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(path => string.Equals(
                        Path.GetFileName(path),
                        "booklogger.db",
                        StringComparison.OrdinalIgnoreCase));

                // Fallback: any .db file
                if (string.IsNullOrEmpty(extractedDbPath))
                {
                    extractedDbPath = Directory
                        .EnumerateFiles(tempExtractDir, "*.db", SearchOption.TopDirectoryOnly)
                        .FirstOrDefault();
                }

                if (string.IsNullOrEmpty(extractedDbPath) || !File.Exists(extractedDbPath))
                {
                    throw new InvalidOperationException("Invalid backup: Missing database file (booklogger.db)");
                }

                progress?.Report("Validating extracted database integrity");
                // Opens read-only: no WAL file created in temp dir
                var backupConnectionString = $"Data Source={extractedDbPath};Mode=ReadOnly";
                await using (var integrityConn = new Microsoft.Data.Sqlite.SqliteConnection(backupConnectionString))
                {
                    await integrityConn.OpenAsync(ct);
                    await using var integrityCmd = integrityConn.CreateCommand();
                    integrityCmd.CommandText = "PRAGMA integrity_check;";
                    var integrityResult = await integrityCmd.ExecuteScalarAsync(ct) as string;
                    if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"The backup database failed its integrity check: {integrityResult}");
                    }
                }

                await using var context = await _contextFactory.CreateDbContextAsync(ct);

                var connectionString = context.Database.GetConnectionString();
                var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
                var currentDbPath = builder.DataSource;

                if (string.IsNullOrWhiteSpace(currentDbPath)) throw new InvalidOperationException("Current database path not found");

                progress?.Report("Closing live DB connections");
                await context.Database.CloseConnectionAsync();

                // Dispose BEFORE copying to release all handles
                await context.DisposeAsync();

                // Clear pool: no stale connections
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                // SQLite can be sticky with locks
                await Task.Delay(200, ct);

                progress?.Report("Swapping DB file");
                File.Copy(extractedDbPath, currentDbPath, true);

                // WAL/SHM from old DB would corrupt the restored one
                var walPath = currentDbPath + "-wal";
                var shmPath = currentDbPath + "-shm";
                if (File.Exists(walPath)) File.Delete(walPath);
                if (File.Exists(shmPath)) File.Delete(shmPath);

                progress?.Report("Applying migrations to restored DB");
                _logger?.LogInformation("Applying migrations to restored database...");
                await using var freshContext = await _contextFactory.CreateDbContextAsync(ct);

                // synchronous=NORMAL with WAL speeds up migrations on slow Android eMMC
                try
                {
                    await freshContext.Database.ExecuteSqlRawAsync("PRAGMA synchronous = NORMAL;", ct);
                    await freshContext.Database.ExecuteSqlRawAsync("PRAGMA temp_store = MEMORY;", ct);
                    DatabaseInitializationHelper.AppendInitLog(
                        "[Restore] [pragma] synchronous=NORMAL, temp_store=MEMORY");
                }
                catch (Exception pragmaEx)
                {
                    DatabaseInitializationHelper.AppendInitLog(
                        $"[Restore] [pragma] failed: {pragmaEx.GetType().Name}: {pragmaEx.Message}");
                }

                // Backup may contain a stale __EFMigrationsLock row from the source device;
                // clear it so EF Core's lock acquisition succeeds on first try.
                await MigrationRecovery.ClearStaleMigrationLockAsync(freshContext, ct);

                var restoreApplied = (await freshContext.Database.GetAppliedMigrationsAsync(ct)).ToList();
                var restorePending = (await freshContext.Database.GetPendingMigrationsAsync(ct)).ToList();
                DatabaseInitializationHelper.AppendInitLog(
                    $"[Restore] applied={restoreApplied.Count} pending={restorePending.Count}");
                if (restoreApplied.Count > 0)
                {
                    DatabaseInitializationHelper.AppendInitLog(
                        $"[Restore] last applied: {restoreApplied[^1]}");
                }
                foreach (var p in restorePending)
                {
                    DatabaseInitializationHelper.AppendInitLog($"[Restore] pending: {p}");
                }

                if (restorePending.Count > 0)
                {
                    var restoreMigrator = freshContext.Database.GetInfrastructure()
                        .GetRequiredService<IMigrator>();
                    MigrationLoggingInterceptor.Enabled = true;
                    try
                    {
                        for (int i = 0; i < restorePending.Count; i++)
                        {
                            var name = restorePending[i];
                            var stepSw = System.Diagnostics.Stopwatch.StartNew();
                            DatabaseInitializationHelper.AppendInitLog(
                                $"[Restore] > [{i + 1}/{restorePending.Count}] {name} ...");
                            progress?.Report($"Migration {i + 1}/{restorePending.Count}: {name}");
                            try
                            {
                                await restoreMigrator.MigrateAsync(name, ct)
                                    .WaitAsync(TimeSpan.FromSeconds(180), ct);
                                stepSw.Stop();
                                DatabaseInitializationHelper.AppendInitLog(
                                    $"[Restore] + [{i + 1}/{restorePending.Count}] {name} OK ({stepSw.ElapsedMilliseconds}ms)");
                            }
                            catch (TimeoutException)
                            {
                                stepSw.Stop();
                                DatabaseInitializationHelper.AppendInitLog(
                                    $"[Restore] ! [{i + 1}/{restorePending.Count}] {name} TIMEOUT after {stepSw.ElapsedMilliseconds}ms");
                                throw new TimeoutException(
                                    $"Migration '{name}' did not finish within 180 seconds during restore. " +
                                    "See Settings → More Info → Diagnostics for the full migration log.");
                            }
                            catch (Exception ex) when (MigrationRecovery.IsSchemaAlreadyAppliedError(ex))
                            {
                                // Schema already present: record as applied and continue
                                stepSw.Stop();
                                DatabaseInitializationHelper.AppendInitLog(
                                    $"[Restore] ~ [{i + 1}/{restorePending.Count}] {name} schema already present " +
                                    $"({ex.GetType().Name}: {ex.Message}); recording as applied");
                                await MigrationRecovery.ForceMarkMigrationAppliedAsync(freshContext, name, ct);
                            }
                            catch (Exception ex)
                            {
                                stepSw.Stop();
                                DatabaseInitializationHelper.AppendInitLog(
                                    $"[Restore] ! [{i + 1}/{restorePending.Count}] {name} FAILED after {stepSw.ElapsedMilliseconds}ms — {ex.GetType().Name}: {ex.Message}");
                                throw;
                            }
                        }
                    }
                    finally
                    {
                        MigrationLoggingInterceptor.Enabled = false;
                    }
                }
                else
                {
                    DatabaseInitializationHelper.AppendInitLog("[Restore] no pending migrations");
                }

                // Entitlements are device-bound: wipe imported rows so DbInitializer re-seeds
                // a Free row on next launch; AppStartup re-queries Play Billing to upgrade if needed.
                if (await freshContext.UserEntitlements.AnyAsync(ct))
                {
                    _logger?.LogInformation("Wiping {Count} imported UserEntitlement rows; they will be re-verified against Play Billing on next startup.",
                        await freshContext.UserEntitlements.CountAsync(ct));
                    freshContext.UserEntitlements.RemoveRange(freshContext.UserEntitlements);
                    await freshContext.SaveChangesAsync(ct);
                }

                progress?.Report("Restoring cover images");
                var extractedCoversDir = Directory
                    .EnumerateDirectories(tempExtractDir, "*", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(path => string.Equals(
                        Path.GetFileName(path),
                        "covers",
                        StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(extractedCoversDir) && Directory.Exists(extractedCoversDir))
                {
                    var targetCoversDir = _fileSystem.CombinePath(_basePath, "covers");

                    // Clear target to match backup exactly (restore = clean slate)
                    if (Directory.Exists(targetCoversDir))
                    {
                        var dirInfo = new DirectoryInfo(targetCoversDir);
                        foreach (var file in dirInfo.GetFiles()) file.Delete();
                        foreach (var dir in dirInfo.GetDirectories()) dir.Delete(true);
                    }
                    else
                    {
                        Directory.CreateDirectory(targetCoversDir);
                    }

                    CopyDirectory(extractedCoversDir, targetCoversDir);
                }

                _logger?.LogInformation("Backup restored successfully");
                progress?.Report("Invalidating settings cache");

                // notifyProgressionChanged: false — app is about to restart; notifying live
                // subscribers while the DB file was just swapped caused them to blow up
                // mid-restore, hiding the successful file ops and preventing auto-restart.
                _appSettingsProvider.InvalidateCache(notifyProgressionChanged: false);

                progress?.Report("Restore complete");
            }
            finally
            {
                if (Directory.Exists(tempExtractDir))
                {
                    Directory.Delete(tempExtractDir, true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to restore from backup");
            throw;
        }
    }

    public async Task DeleteAllDataAsync(CancellationToken ct = default)
    {
        try
        {
            _logger?.LogWarning("Deleting all user data");

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Delete in FK-safe order: junction/child entities first
            context.Annotations.RemoveRange(context.Annotations);
            context.Quotes.RemoveRange(context.Quotes);
            context.ReadingSessions.RemoveRange(context.ReadingSessions);
            context.BookGenres.RemoveRange(context.BookGenres);
            context.BookTropes.RemoveRange(context.BookTropes);
            context.BookShelves.RemoveRange(context.BookShelves);
            context.PlantShelves.RemoveRange(context.PlantShelves);
            // DecorationShelves → UserDecorations → ShopItems (cascade/restrict ordering)
            context.DecorationShelves.RemoveRange(context.DecorationShelves);
            context.UserDecorations.RemoveRange(context.UserDecorations);
            context.WishlistInfos.RemoveRange(context.WishlistInfos);
            context.GoalExcludedBooks.RemoveRange(context.GoalExcludedBooks);
            context.GoalGenres.RemoveRange(context.GoalGenres);
            context.OnboardingMissionStates.RemoveRange(context.OnboardingMissionStates);

            context.Books.RemoveRange(context.Books);
            context.ReadingGoals.RemoveRange(context.ReadingGoals);
            context.Shelves.RemoveRange(context.Shelves);
            context.UserPlants.RemoveRange(context.UserPlants);
            context.ShopItems.RemoveRange(context.ShopItems);

            var settings = await context.AppSettings.FirstOrDefaultAsync(ct);
            if (settings != null)
            {
                settings.UserLevel = 1;
                settings.TotalXp = 0;
                settings.Coins = 100;
                settings.PlantsPurchased = 0;
                settings.LastBackupDate = null;
                settings.HasCompletedOnboarding = false;
                settings.OnboardingFlowVersion = OnboardingMissionCatalog.CurrentFlowVersion;
                settings.OnboardingIntroStatus = OnboardingIntroStatus.NotStarted;
                settings.OnboardingCurrentStep = 0;
                settings.OnboardingCompletedAt = null;
                settings.OnboardingAutoCompletedForExistingUser = false;
                settings.OnboardingTutorialPlantId = null;
                settings.OnboardingTutorialPlantNeedsWateringAssist = false;
                settings.UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(ct);

            _appSettingsProvider.InvalidateCache();

            _logger?.LogWarning("All user data deleted successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete all data");
            throw;
        }
    }

    private class BookCsvRecord
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string? ISBN { get; set; }
        public string? Publisher { get; set; }
        public int? PublicationYear { get; set; }
        public string? Language { get; set; }
        public string? Description { get; set; }
        public int? PageCount { get; set; }
        public int CurrentPage { get; set; }
        public string? CoverImagePath { get; set; }
        public string Status { get; set; } = "Planned";
        public double? Rating { get; set; }
        public DateTime? DateAdded { get; set; }
        public DateTime? DateStarted { get; set; }
        public DateTime? DateCompleted { get; set; }
        public string? Genres { get; set; }
    }
}
