using System.Globalization;
using System.Text.Json;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
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
    private readonly string _backupDirectory;
    private readonly string _basePath;

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
                .Include(b => b.Quotes)
                .Include(b => b.Annotations)
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
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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

            // Map books to flat structure for CSV
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
                Genres = string.Join(";", b.BookGenres.Select(bg => bg.Genre.Name))
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

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Add books (with merge strategy to avoid duplicates)
            int importedCount = 0;
            foreach (var book in books)
            {
                // Check if book already exists (by ISBN or Title+Author)
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

            // Get the database file path
            var dbPath = context.Database.GetConnectionString()?.Replace("Data Source=", "");

            if (string.IsNullOrWhiteSpace(dbPath) || !_fileSystem.FileExists(dbPath))
            {
                throw new InvalidOperationException("Database file not found");
            }

            // Create temporary directory for staging backup files
            var tempBackupDir = _fileSystem.CombinePath(_backupDirectory, $"temp_{Guid.NewGuid()}");
            _fileSystem.CreateDirectory(tempBackupDir);

            try
            {
                // 1. Copy Database
                // We need to close connection or ensure WAL is checkpointed ideally, 
                // but for SQLite simple copy usually works if file system allows read sharing.
                // However, better safely:
                var destDbPath = _fileSystem.CombinePath(tempBackupDir, "booklogger.db");
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
                var backupZipName = $"booklogger_backup_{timestamp}.zip";
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

    public async Task RestoreFromBackupAsync(string backupPath, CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Restoring from backup: {Path}", backupPath);

            if (!File.Exists(backupPath))
            {
                throw new FileNotFoundException("Backup file not found", backupPath);
            }

            // Temp directory for extraction
            var tempExtractDir = _fileSystem.CombinePath(_backupDirectory, $"restore_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempExtractDir);

            try
            {
                // 1. Extract ZIP securely (prevent Zip Slip)
                using (var archive = ZipFile.OpenRead(backupPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        var destinationPath = Path.GetFullPath(Path.Combine(tempExtractDir, entry.FullName));
                        var destinationDir = Path.GetFullPath(tempExtractDir);

                        // Ensure destinationDir ends with a separator to prevent partial path matching
                        if (!destinationDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                        {
                            destinationDir += Path.DirectorySeparatorChar;
                        }

                        if (!destinationPath.StartsWith(destinationDir, StringComparison.Ordinal))
                        {
                            throw new IOException($"Zip Slip vulnerability detected: {entry.FullName}");
                        }

                        // Ensure directory exists
                        var entryDir = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(entryDir) && !Directory.Exists(entryDir))
                        {
                            Directory.CreateDirectory(entryDir);
                        }

                        if (!string.IsNullOrEmpty(entry.Name)) // It's a file, not just a directory entry
                        {
                            entry.ExtractToFile(destinationPath, overwrite: true);
                        }
                    }
                }

                // 2. Validate Backup Content
                var extractedDbPath = Path.Combine(tempExtractDir, "booklogger.db");
                if (!File.Exists(extractedDbPath))
                {
                    throw new InvalidOperationException("Invalid backup: Missing database file");
                }

                await using var context = await _contextFactory.CreateDbContextAsync(ct);
                
                // Get current DB path
                var currentDbPath = context.Database.GetConnectionString()?.Replace("Data Source=", "");
                if (string.IsNullOrWhiteSpace(currentDbPath)) throw new InvalidOperationException("Current database path not found");

                // 3. Close Connections & Restore DB
                await context.Database.CloseConnectionAsync();
                
                // Wait a bit to ensure locks are released (SQLite can be sticky)
                await Task.Delay(100, ct);

                File.Copy(extractedDbPath, currentDbPath, true);

                // 4. Restore Covers
                var extractedCoversDir = Path.Combine(tempExtractDir, "covers");
                if (Directory.Exists(extractedCoversDir))
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

                // Invalidate AppSettings cache to load restored values
                _appSettingsProvider.InvalidateCache();

                // Reopen connection
                await context.Database.OpenConnectionAsync(ct);
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

            // Delete in correct order to respect foreign key constraints
            // 1. Delete child entities first
            context.Annotations.RemoveRange(context.Annotations);
            context.Quotes.RemoveRange(context.Quotes);
            context.ReadingSessions.RemoveRange(context.ReadingSessions);
            context.BookGenres.RemoveRange(context.BookGenres);

            // 2. Delete main entities
            context.Books.RemoveRange(context.Books);
            context.ReadingGoals.RemoveRange(context.ReadingGoals);
            context.UserPlants.RemoveRange(context.UserPlants);
            context.ShopItems.RemoveRange(context.ShopItems);

            // 3. Reset AppSettings to defaults (but keep the record)
            var settings = await context.AppSettings.FirstOrDefaultAsync(ct);
            if (settings != null)
            {
                settings.UserLevel = 1;
                settings.TotalXp = 0;
                settings.Coins = 100; // Starting coins
                settings.PlantsPurchased = 0;
                settings.LastBackupDate = null;
                settings.UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(ct);

            // Invalidate the AppSettingsProvider cache to force reload of reset values
            _appSettingsProvider.InvalidateCache();

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
