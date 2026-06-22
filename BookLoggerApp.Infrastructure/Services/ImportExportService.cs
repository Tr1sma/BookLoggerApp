using System.Data;
using System.Globalization;
using System.Text.Json;
using BookLoggerApp.Core.Entitlements;
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

/// <summary>
/// Service for importing and exporting data in various formats.
/// Uses DbContextFactory for thread-safe operations.
/// </summary>
public class ImportExportService : IImportExportService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<ImportExportService>? _logger;
    private readonly IFileSystem _fileSystem;
    private readonly IAppSettingsProvider _appSettingsProvider;
    private readonly IEntitlementService? _entitlementService;
    private readonly string _backupDirectory;
    private readonly string _basePath;

    // Security constraints for zip extraction (Zip Bomb protection)
    private const long MaxTotalExtractionSize = 1024L * 1024L * 1024L; // 1 GB
    private const int MaxEntryCount = 10000;

    // BUG-11: suffix for the pre-restore rollback snapshot of the live DB + sidecars.
    private const string RollbackSnapshotSuffix = ".pre-restore.bak";
    private static readonly string[] DbFileSuffixes = { "", "-wal", "-shm" };

    public ImportExportService(
        IDbContextFactory<AppDbContext> contextFactory,
        IFileSystem fileSystem,
        IAppSettingsProvider appSettingsProvider,
        ILogger<ImportExportService>? logger = null,
        string? appDataPath = null,
        IEntitlementService? entitlementService = null)
    {
        _contextFactory = contextFactory;
        _fileSystem = fileSystem;
        _appSettingsProvider = appSettingsProvider;
        _entitlementService = entitlementService;
        _logger = logger;

        // Set up backup directory
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

            // Fetch all data
            var books = await context.Books
                .Include(b => b.BookGenres)
                    .ThenInclude(bg => bg.Genre)
                .Include(b => b.ReadingSessions)
                    .ThenInclude(rs => rs.Moods)
                .Include(b => b.Quotes)
                .Include(b => b.Annotations)
                .Include(b => b.WishlistInfo)
                .ToListAsync(ct);

            var goals = await context.ReadingGoals.ToListAsync(ct);
            var plants = await context.UserPlants
                .Include(p => p.Species)
                .ToListAsync(ct);
            var settings = await context.AppSettings.FirstOrDefaultAsync(ct);

            // Create export object
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

            // Map books to flat structure for CSV. Free-text fields are passed through
            // SanitizeCsvField so a value beginning with =, +, -, @ or a control char
            // cannot be interpreted as a formula when the export is opened in a
            // spreadsheet (CSV/formula injection — SEC-14). Title/Author/Description may
            // contain untrusted Google-Books data, so they are not safe to write verbatim.
            var flatBooks = books.Select(b => new
            {
                b.Id,
                Title = SanitizeCsvField(b.Title),
                Author = SanitizeCsvField(b.Author),
                ISBN = SanitizeCsvField(b.ISBN),
                Publisher = SanitizeCsvField(b.Publisher),
                b.PublicationYear,
                Language = SanitizeCsvField(b.Language),
                Description = SanitizeCsvField(b.Description),
                b.PageCount,
                b.CurrentPage,
                CoverImagePath = SanitizeCsvField(b.CoverImagePath),
                Status = b.Status.ToString(),
                Rating = b.AverageRating,
                b.DateAdded,
                b.DateStarted,
                b.DateCompleted,
                Genres = SanitizeCsvField(string.Join(";", b.BookGenres.Where(bg => bg.Genre != null).Select(bg => bg.Genre.Name)))
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

            // Deserialize to a dynamic object first to handle the structure
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

            // Add books with a merge strategy. Each book is imported in its OWN context and
            // try/catch so a single failure (e.g. an unexpected constraint) skips just that
            // book instead of aborting the whole batch (BUG-03).
            int importedCount = 0;
            foreach (var book in books)
            {
                try
                {
                    await using var context = await _contextFactory.CreateDbContextAsync(ct);

                    // Check if book already exists (by ISBN or Title+Author)
                    var exists = await context.Books.AnyAsync(b =>
                        (b.ISBN != null && b.ISBN == book.ISBN) ||
                        (b.Title == book.Title && b.Author == book.Author), ct);

                    if (exists)
                    {
                        _logger?.LogInformation("Book already exists, skipping: {Title} by {Author}",
                            book.Title, book.Author);
                        continue;
                    }

                    // Reassign fresh primary keys to the book + its owned children and resolve
                    // genres by name (find-or-create) so the import never re-inserts seeded
                    // Genre rows or collides with existing child PKs (BUG-03).
                    PrepareImportedBookGraph(book);
                    await ResolveImportedGenresAsync(context, book, ct);

                    // HIGH-1003: a backup/import from a higher tier must not reintroduce the
                    // Plus-only Wishlist metadata for a user who is not entitled to it. The
                    // service-layer guards (SEC-16) only cover live writes; import bypasses them.
                    if (book.WishlistInfo is not null
                        && _entitlementService is not null
                        && !await _entitlementService.HasAccessAsync(FeatureKey.Wishlist, ct))
                    {
                        book.WishlistInfo = null;
                    }

                    context.Books.Add(book);
                    await context.SaveChangesAsync(ct);
                    importedCount++;
                }
                catch (Exception bookEx)
                {
                    _logger?.LogWarning(bookEx,
                        "Skipping book '{Title}' during JSON import due to an error", book.Title);
                }
            }

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

            // Pre-load existing genres for efficient find-or-create
            var existingGenres = await context.Genres.ToListAsync(ct);
            var genreLookup = existingGenres.ToDictionary(g => g.Name, StringComparer.OrdinalIgnoreCase);

            int importedCount = 0;
            foreach (var record in records)
            {
                // Check if book already exists
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

                    // Restore genre associations from CSV
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

            // Get the database file path safely
            var connectionString = context.Database.GetConnectionString();
            var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
            var dbPath = builder.DataSource;

            if (string.IsNullOrWhiteSpace(dbPath) || !_fileSystem.FileExists(dbPath))
            {
                throw new InvalidOperationException("Database file not found");
            }

            // Create temporary directory for staging backup files
            var tempBackupDir = _fileSystem.CombinePath(_backupDirectory, $"temp_{Guid.NewGuid()}");
            _fileSystem.CreateDirectory(tempBackupDir);

            try
            {
                // Z.699: checkpoint the WAL into the main DB file before copying so the backup is
                // self-contained and doesn't depend on a separate -wal/-shm sidecar.
                var destDbPath = _fileSystem.CombinePath(tempBackupDir, "booklogger.db");
                try { await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);", ct); } catch { }
                _fileSystem.CopyFile(dbPath, destDbPath, overwrite: true);

                // 2. Copy Covers
                var coversSourceDir = _fileSystem.CombinePath(_basePath, "covers");
                var coversDestDir = _fileSystem.CombinePath(tempBackupDir, "covers");

                if (_fileSystem.DirectoryExists(coversSourceDir))
                {
                    _fileSystem.CreateDirectory(coversDestDir);
                    // Manually copy files since IFileSystem might not have recursive copy
                    // Assuming flat structure for covers as per ImageService
                    // Validating ImageService implementation: it puts files directly in 'covers' dir.
                    // We need to check if IFileSystem exposes GetFiles, if not we might need to rely on System.IO or concrete implementation.
                    // Checking ImportExportService dependencies: it uses IFileSystem.
                    
                    // NOTE: Since IFileSystem abstraction might be limited, and we are in Infrastructure which has access to System.IO,
                    // we can use standard DirectoryInfo if IFileSystem is too restrictive, BUT better to stick to injection if possible. 
                    // However, standard System.IO.Compression.ZipFile.CreateFromDirectory works on file system paths.
                    
                    // Let's use the actual file system for the directory copy to be safe and simple 
                    // since ZipFile.CreateFromDirectory is a static system method anyway.
                    
                    // We can't rely solely on _fileSystem interface for the ZipFile helper which requires string paths.
                    // So we will assume standard IO access is permitted for these paths.
                    
                    CopyDirectory(coversSourceDir, coversDestDir);
                }

                // 3. Create ZIP
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupZipName = $"bookheart_backup_{timestamp}.zip";
                var backupZipPath = _fileSystem.CombinePath(_backupDirectory, backupZipName);

                // Ensure backup zip doesn't exist
                if (File.Exists(backupZipPath)) File.Delete(backupZipPath);

                ZipFile.CreateFromDirectory(tempBackupDir, backupZipPath);

                _logger?.LogInformation("Backup created at: {Path}", backupZipPath);

                // Update AppSettings
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
                // Cleanup temp dir
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

    /// <summary>
    /// Reassigns fresh primary keys to an imported book and its owned children, and rewires
    /// the child foreign keys, so importing into a non-empty DB (or re-importing the same
    /// export) can never collide with existing rows. Genres are resolved separately
    /// (find-or-create) because they are shared/seed entities. See code review BUG-03.
    /// </summary>
    private static void PrepareImportedBookGraph(Book book)
    {
        book.Id = Guid.NewGuid();

        foreach (var session in book.ReadingSessions)
        {
            session.Id = Guid.NewGuid();
            session.BookId = book.Id;
            // Moods have a composite PK (ReadingSessionId, Mood); point them at the new
            // session id so they don't carry the source device's key.
            foreach (var mood in session.Moods)
            {
                mood.ReadingSessionId = session.Id;
            }
        }

        foreach (var quote in book.Quotes)
        {
            quote.Id = Guid.NewGuid();
            quote.BookId = book.Id;
        }

        foreach (var annotation in book.Annotations)
        {
            annotation.Id = Guid.NewGuid();
            annotation.BookId = book.Id;
        }

        if (book.WishlistInfo is not null)
        {
            book.WishlistInfo.BookId = book.Id; // PK == FK for the 1:1
        }
    }

    /// <summary>
    /// Replaces the imported book's carried Genre entities (which keep their original seed
    /// primary keys) with find-or-create resolutions against the target DB, so a seeded
    /// genre is never re-inserted (PK collision) and free-form genres are de-duplicated by
    /// name — mirroring the CSV import path. See code review BUG-03.
    /// </summary>
    private static async Task ResolveImportedGenresAsync(AppDbContext context, Book book, CancellationToken ct)
    {
        if (book.BookGenres.Count == 0)
        {
            return;
        }

        var existingGenres = await context.Genres.ToListAsync(ct);
        var lookup = existingGenres.ToDictionary(g => g.Name, StringComparer.OrdinalIgnoreCase);

        var resolved = new List<BookGenre>();
        var linkedGenreIds = new HashSet<Guid>();
        foreach (var bookGenre in book.BookGenres)
        {
            var name = bookGenre.Genre?.Name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue; // can't resolve a genre we don't have a name for
            }

            if (!lookup.TryGetValue(name, out var genre))
            {
                genre = new Genre { Name = name };
                context.Genres.Add(genre);
                lookup[name] = genre;
            }

            // Skip duplicate links to the same genre (composite PK BookId+GenreId).
            if (!linkedGenreIds.Add(genre.Id) && genre.Id != Guid.Empty)
            {
                continue;
            }

            resolved.Add(new BookGenre
            {
                BookId = book.Id,
                GenreId = genre.Id,
                Genre = genre,
                AddedAt = bookGenre.AddedAt
            });
        }

        book.BookGenres = resolved;
    }

    /// <summary>
    /// Neutralizes CSV/formula injection: if <paramref name="value"/> begins with a
    /// character a spreadsheet may interpret as a formula (=, +, -, @) or a control
    /// character (Tab, CR, LF), it is prefixed with a single apostrophe so Excel /
    /// Google Sheets / LibreOffice render it as literal text. See code review SEC-14.
    /// </summary>
    internal static string? SanitizeCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        char first = value[0];
        if (first is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n')
        {
            return "'" + value;
        }

        return value;
    }

    /// <summary>
    /// Copies <paramref name="source"/> to <paramref name="destination"/> while counting
    /// the bytes actually transferred. Throws <see cref="IOException"/> the moment the
    /// running total would exceed <paramref name="maxBytes"/>. This is the authoritative
    /// Zip-Bomb guard: it measures real decompressed output rather than trusting the
    /// archive's declared (and forgeable) entry length. Returns the number of bytes written.
    /// See code review SEC-05.
    /// </summary>
    internal static long CopyStreamWithLimit(Stream source, Stream destination, long maxBytes)
    {
        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new IOException(
                    "Zip Bomb detected: decompressed content exceeds the total extraction size limit.");
            }
            destination.Write(buffer, 0, read);
        }
        return total;
    }

    /// <summary>
    /// Copies the live DB and its WAL/SHM sidecars to side-by-side <c>.pre-restore.bak</c>
    /// files so a failed restore can be rolled back. See code review BUG-11.
    /// </summary>
    private static void CreateRollbackSnapshot(string dbPath)
    {
        foreach (var suffix in DbFileSuffixes)
        {
            var src = dbPath + suffix;
            if (File.Exists(src))
            {
                File.Copy(src, src + RollbackSnapshotSuffix, overwrite: true);
            }
        }
    }

    /// <summary>
    /// Restores the DB and sidecars previously captured by <see cref="CreateRollbackSnapshot"/>.
    /// Any sidecar that did not exist pre-restore is removed so an old DB is never paired
    /// with a newer WAL/SHM produced by the failed attempt.
    /// </summary>
    private static void RestoreRollbackSnapshot(string dbPath)
    {
        foreach (var suffix in DbFileSuffixes)
        {
            var target = dbPath + suffix;
            var snapshot = target + RollbackSnapshotSuffix;
            if (File.Exists(snapshot))
            {
                File.Copy(snapshot, target, overwrite: true);
            }
            else if (File.Exists(target))
            {
                try { File.Delete(target); } catch { /* best effort */ }
            }
        }
    }

    /// <summary>Deletes the rollback snapshot files created by <see cref="CreateRollbackSnapshot"/>.</summary>
    private static void DeleteRollbackSnapshot(string dbPath)
    {
        foreach (var suffix in DbFileSuffixes)
        {
            var snapshot = dbPath + suffix + RollbackSnapshotSuffix;
            if (File.Exists(snapshot))
            {
                try { File.Delete(snapshot); } catch { /* best effort */ }
            }
        }
    }

    private void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) return; // Should have been checked

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

            // Gate on DbInitializer completion so we never swap the DB file out
            // from under an in-flight scoped DbContext in the startup initializer.
            // Defense in depth: the ViewModel is expected to have awaited this
            // already, but any direct caller (tests, future Android intents) gets
            // the same guarantee. The second await is a no-op once the TCS is set.
            progress?.Report("Waiting for database initialization to complete");
            await BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.EnsureInitializedAsync();
            progress?.Report("Database initialization confirmed");

            // DbInitializer's deferred maintenance (RunDeferredMaintenanceAsync) runs
            // fire-and-forget AFTER the init gate is signalled and keeps writing through its
            // OWN context. Wait for it to finish before swapping the file so that background
            // writer is not a surviving second connection across the swap — the root cause of
            // SQLITE_CORRUPT "database disk image is malformed". Best-effort: proceeds after a
            // timeout rather than blocking the restore forever.
            progress?.Report("Waiting for background maintenance to finish");
            await BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper
                .EnsureDeferredMaintenanceCompleteAsync(TimeSpan.FromSeconds(20));

            // Block the Android widget's own (non-DI, un-poolable) connection for the duration
            // of the restore so a BroadcastReceiver refresh cannot open booklogger.db3 mid-swap.
            // Cleared in the finally below regardless of outcome.
            BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.BeginRestore();

            if (!File.Exists(backupPath))
            {
                throw new FileNotFoundException("Backup file not found", backupPath);
            }

            // Temp directory for extraction
            var tempExtractDir = _fileSystem.CombinePath(_backupDirectory, $"restore_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempExtractDir);

            try
            {
                progress?.Report("Extracting ZIP archive");
                // 1. Extract ZIP securely
                // Sentinel: Manual extraction with path validation to prevent Zip Slip and Zip Bomb vulnerabilities
                using (var archive = ZipFile.OpenRead(backupPath))
                {
                    long totalExtracted = 0;
                    int entryCount = 0;

                    foreach (var entry in archive.Entries)
                    {
                        // Check for Zip Bomb (too many entries)
                        entryCount++;
                        if (entryCount > MaxEntryCount)
                        {
                            throw new IOException("Zip Bomb detected: Too many entries in archive.");
                        }

                        var destinationPath = Path.GetFullPath(Path.Combine(tempExtractDir, entry.FullName));
                        var tempDirFullPath = Path.GetFullPath(tempExtractDir);

                        if (!tempDirFullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                        {
                            tempDirFullPath += Path.DirectorySeparatorChar;
                        }

                        // Check for Zip Slip
                        if (!destinationPath.StartsWith(tempDirFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new IOException($"Zip Slip vulnerability detected: {entry.FullName}");
                        }

                        // Create directory if it doesn't exist (entry.FullName might contain directories)
                        if (entry.Name == "")
                        {
                            // It's a directory
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                            // Zip Bomb protection (SEC-05): cap the ACTUAL decompressed bytes
                            // written across the whole archive, not the entry.Length header.
                            // A crafted archive can declare a tiny uncompressed size yet
                            // inflate to gigabytes; the only reliable defense counts the bytes
                            // as they are written through a limiting copy.
                            long remainingBudget = MaxTotalExtractionSize - totalExtracted;
                            using (var entryStream = entry.Open())
                            using (var fileStream = File.Create(destinationPath))
                            {
                                totalExtracted += CopyStreamWithLimit(entryStream, fileStream, remainingBudget);
                            }
                        }
                    }
                }

                // 2. Validate Backup Content
                // Case-insensitive search for database file
                var extractedDbPath = Directory
                    .EnumerateFiles(tempExtractDir, "*", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(path => string.Equals(
                        Path.GetFileName(path),
                        "booklogger.db",
                        StringComparison.OrdinalIgnoreCase));

                // If not found, try any .db file (fallback)
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
                // Validate the extracted database before overwriting the live database.
                // Opens it read-only so no WAL file is created in the temp directory.
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
                
                // Get current DB path safeley
                var connectionString = context.Database.GetConnectionString();
                var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
                var currentDbPath = builder.DataSource;
                
                if (string.IsNullOrWhiteSpace(currentDbPath)) throw new InvalidOperationException("Current database path not found");

                progress?.Report("Closing live DB connections");
                // 3. Close Connections & Restore DB
                await context.Database.CloseConnectionAsync();

                // Dispose the context BEFORE copying to release all handles
                await context.DisposeAsync();

                // Clear SQLite connection pool to ensure no stale connections
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                // Wait a bit to ensure locks are released (SQLite can be sticky)
                await Task.Delay(200, ct);

                // BUG-11: snapshot the live DB (and any WAL/SHM sidecars) BEFORE overwriting
                // it, so a failure during the file swap or the subsequent migration can be
                // rolled back. Without this, a failed restore-migration leaves the user with
                // an irreversibly overwritten, half-migrated database.
                var walPath = currentDbPath + "-wal";
                var shmPath = currentDbPath + "-shm";
                CreateRollbackSnapshot(currentDbPath);

                try
                {
                    progress?.Report("Swapping DB file");
                    // Delete the live WAL/SHM sidecars BEFORE overwriting the main file. A stale
                    // wal-index left next to a freshly-copied main file is the root cause of the
                    // "database disk image is malformed" failure; SQLite rebuilds clean sidecars
                    // for the next connection on first access, so removing them first is safe.
                    if (File.Exists(walPath)) File.Delete(walPath);
                    if (File.Exists(shmPath)) File.Delete(shmPath);

                    File.Copy(extractedDbPath, currentDbPath, true);

                    // Belt-and-suspenders: drop any sidecar that reappeared between the deletes
                    // and the copy so the fresh context opens against a clean file.
                    if (File.Exists(walPath)) File.Delete(walPath);
                    if (File.Exists(shmPath)) File.Delete(shmPath);

                    progress?.Report("Applying migrations to restored DB");
                    // Run migrations on a FRESH, NON-POOLED context after the file copy. We do NOT
                    // use _contextFactory here: in production its connection string has pooling ON,
                    // so the connection that performs the migration write burst could linger in the
                    // pool and be reused against the swapped file. Pooling=false guarantees this
                    // connection is fully closed on dispose. Per-migration logging mirrors
                    // DbInitializer.MigrateDatabaseAsync so a hung migration is identifiable from
                    // Settings → More Info → Diagnostics. A backup may come from an older schema
                    // version, so the pending list can be long here even when nothing is wrong.
                    _logger?.LogInformation("Applying migrations to restored database...");
                    var restoreConnectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                    {
                        DataSource = currentDbPath,
                        Pooling = false
                    }.ToString();
                    var restoreOptions = new DbContextOptionsBuilder<AppDbContext>()
                        .UseSqlite(restoreConnectionString)
                        .Options;
                    await using var freshContext = new AppDbContext(restoreOptions);

                    // Re-establish WAL deterministically on the freshly-copied file (do not rely on
                    // the backup's persisted header), mirroring DbInitializer.SetJournalModeWalAsync,
                    // then normalize the WAL state. synchronous=NORMAL with WAL is safe and speeds up
                    // multi-statement migrations on slow Android eMMC. Each step is BUSY-tolerant so a
                    // momentarily-open foreign connection cannot turn a recoverable restore into a
                    // hard failure.
                    try
                    {
                        string journalMode = "unknown";
                        var restoreConn = freshContext.Database.GetDbConnection();
                        if (restoreConn.State != ConnectionState.Open)
                        {
                            await restoreConn.OpenAsync(ct);
                        }
                        await using (var jmCmd = restoreConn.CreateCommand())
                        {
                            jmCmd.CommandText = "PRAGMA journal_mode = WAL;";
                            journalMode = (await jmCmd.ExecuteScalarAsync(ct))?.ToString() ?? "unknown";
                        }
                        try
                        {
                            await freshContext.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", ct);
                        }
                        catch (Exception cpEx)
                        {
                            DatabaseInitializationHelper.AppendInitLog(
                                $"[Restore] [pragma] wal_checkpoint skipped: {cpEx.GetType().Name}: {cpEx.Message}");
                        }
                        await freshContext.Database.ExecuteSqlRawAsync("PRAGMA synchronous = NORMAL;", ct);
                        await freshContext.Database.ExecuteSqlRawAsync("PRAGMA temp_store = MEMORY;", ct);
                        DatabaseInitializationHelper.AppendInitLog(
                            $"[Restore] [pragma] journal_mode={journalMode}, synchronous=NORMAL, temp_store=MEMORY");
                    }
                    catch (Exception pragmaEx)
                    {
                        DatabaseInitializationHelper.AppendInitLog(
                            $"[Restore] [pragma] failed: {pragmaEx.GetType().Name}: {pragmaEx.Message}");
                    }

                    // The backup we just restored may itself contain a stale
                    // __EFMigrationsLock row from the source device. Clear it before
                    // calling MigrateAsync so EF Core's lock acquisition succeeds on
                    // first try instead of polling.
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
                        // Surface every SQL command issued by EF Core during these migrations
                        // to the InitLog. Toggle is reset in finally even if a migration throws.
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
                                    // Same recovery path as DbInitializer: the migration's
                                    // ALTER TABLE / CREATE TABLE found pre-existing schema.
                                    // Mark applied and continue so the user isn't blocked.
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

                    // Entitlement state is device-bound: it reflects what Google Play says
                    // THIS device owns, not what the backup source had. Wipe the UserEntitlement
                    // rows from the restored DB so DbInitializer re-seeds a Free row on the next
                    // app-launch; AppStartup then re-queries Play Billing and upgrades if the
                    // Google account has an active subscription.
                    if (await freshContext.UserEntitlements.AnyAsync(ct))
                    {
                        _logger?.LogInformation("Wiping {Count} imported UserEntitlement rows; they will be re-verified against Play Billing on next startup.",
                            await freshContext.UserEntitlements.CountAsync(ct));
                        freshContext.UserEntitlements.RemoveRange(freshContext.UserEntitlements);
                        await freshContext.SaveChangesAsync(ct);
                    }
                }
                catch
                {
                    // BUG-11 rollback: a failed swap or migration must not leave the user with
                    // a half-migrated or wrong-schema database. The freshContext above is
                    // disposed as the exception unwinds (await using), so the file is unlocked
                    // here — restore the pre-restore snapshot before propagating the error.
                    DatabaseInitializationHelper.AppendInitLog(
                        "[Restore] swap/migration failed — rolling back to the pre-restore database");
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    RestoreRollbackSnapshot(currentDbPath);
                    throw;
                }
                finally
                {
                    DeleteRollbackSnapshot(currentDbPath);
                }

                progress?.Report("Restoring cover images");
                // 4. Restore Covers
                // Case-insensitive search for covers directory
                var extractedCoversDir = Directory
                    .EnumerateDirectories(tempExtractDir, "*", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(path => string.Equals(
                        Path.GetFileName(path),
                        "covers",
                        StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(extractedCoversDir) && Directory.Exists(extractedCoversDir))
                {
                    var targetCoversDir = _fileSystem.CombinePath(_basePath, "covers");

                    // Clean target covers dir to remove orphaned images? 
                    // User requested "Restore", usually enabling a clean slate or overwrite.
                    // Let's clear target directory to ensure exact match with backup.
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

                // Invalidate AppSettings cache without firing ProgressionChanged:
                // the app is about to restart, and notifying live subscribers while
                // the DB file was just swapped under their feet caused them to blow
                // up mid-restore (the whole restore was being rolled back into a
                // generic "Failed to restore backup" error, hiding the successful
                // file ops and preventing the auto-restart).
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
        finally
        {
            // Always re-enable widget DB access, even on failure (the success path restarts
            // the app shortly after, but the failure path keeps running).
            BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.EndRestore();
        }
    }

    public async Task DeleteAllDataAsync(CancellationToken ct = default)
    {
        try
        {
            _logger?.LogWarning("Deleting all user data");

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Delete in correct order to respect foreign key constraints
            // 1. Delete junction/child entities first
            context.Annotations.RemoveRange(context.Annotations);
            context.Quotes.RemoveRange(context.Quotes);
            context.ReadingSessions.RemoveRange(context.ReadingSessions);
            context.BookGenres.RemoveRange(context.BookGenres);
            context.BookTropes.RemoveRange(context.BookTropes);
            context.BookShelves.RemoveRange(context.BookShelves);
            context.PlantShelves.RemoveRange(context.PlantShelves);
            // DecorationShelves references UserDecoration (Cascade) and Shelf — must go
            // before UserDecorations, which in turn has DeleteBehavior.Restrict on its
            // ShopItem FK, so UserDecorations must be gone before ShopItems are removed
            // below, otherwise the SaveChanges below fails with an FK violation once the
            // user has purchased any decoration.
            context.DecorationShelves.RemoveRange(context.DecorationShelves);
            context.UserDecorations.RemoveRange(context.UserDecorations);
            context.WishlistInfos.RemoveRange(context.WishlistInfos);
            context.GoalExcludedBooks.RemoveRange(context.GoalExcludedBooks);
            context.GoalGenres.RemoveRange(context.GoalGenres);
            context.OnboardingMissionStates.RemoveRange(context.OnboardingMissionStates);

            // 2. Delete main entities
            context.Books.RemoveRange(context.Books);
            context.ReadingGoals.RemoveRange(context.ReadingGoals);
            context.Shelves.RemoveRange(context.Shelves);
            context.UserPlants.RemoveRange(context.UserPlants);
            context.ShopItems.RemoveRange(context.ShopItems);

            // Persist all the entity deletions.
            await context.SaveChangesAsync(ct);

            // 3. Reset AppSettings to defaults (but keep the record) through the serialized provider
            // (CODE_REVIEW BUG-08): a raw context write here would bypass AppSettingsProvider's
            // write-gate and could race a concurrent coin/XP/level award. Routing the reset through
            // UpdateSettingsAsync serialises it and refreshes the provider cache (so a separate
            // InvalidateCache is no longer needed).
            var settings = await _appSettingsProvider.GetSettingsAsync(ct);
            settings.UserLevel = 1;
            settings.TotalXp = 0;
            settings.Coins = 100; // Starting coins
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
            await _appSettingsProvider.UpdateSettingsAsync(settings, ct);

            _logger?.LogWarning("All user data deleted successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete all data");
            throw;
        }
    }

    // Helper class for CSV import/export
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
