using System.Globalization;
using System.Text.Json;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    private readonly string _backupDirectory;

    public ImportExportService(
        IDbContextFactory<AppDbContext> contextFactory,
        IFileSystem fileSystem,
        ILogger<ImportExportService>? logger = null)
    {
        _contextFactory = contextFactory;
        _fileSystem = fileSystem;
        _logger = logger;

        // Set up backup directory
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _backupDirectory = _fileSystem.CombinePath(appDataPath, "backups");
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
                Rating = b.OverallRating,
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
                        OverallRating = record.Rating,
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
            _logger?.LogInformation("Creating backup");

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Get the database file path
            var dbPath = context.Database.GetConnectionString()?.Replace("Data Source=", "");

            if (string.IsNullOrWhiteSpace(dbPath) || !_fileSystem.FileExists(dbPath))
            {
                throw new InvalidOperationException("Database file not found");
            }

            // Create backup filename with timestamp
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFileName = $"booklogger_backup_{timestamp}.db";
            var backupPath = _fileSystem.CombinePath(_backupDirectory, backupFileName);

            // Copy database file
            _fileSystem.CopyFile(dbPath, backupPath, overwrite: true);

            _logger?.LogInformation("Backup created at: {Path}", backupPath);

            // Update AppSettings with last backup date
            var settings = await context.AppSettings.FirstOrDefaultAsync(ct);
            if (settings != null)
            {
                settings.LastBackupDate = DateTime.UtcNow;
                await context.SaveChangesAsync(ct);
            }

            return backupPath;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create backup");
            throw;
        }
    }

    public async Task RestoreFromBackupAsync(string backupPath, CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Restoring from backup: {Path}", backupPath);

            if (!_fileSystem.FileExists(backupPath))
            {
                throw new FileNotFoundException("Backup file not found", backupPath);
            }

            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Get the current database file path
            var dbPath = context.Database.GetConnectionString()?.Replace("Data Source=", "");

            if (string.IsNullOrWhiteSpace(dbPath))
            {
                throw new InvalidOperationException("Database path not found");
            }

            // Close all connections
            await context.Database.CloseConnectionAsync();

            // Copy backup file over current database
            _fileSystem.CopyFile(backupPath, dbPath, overwrite: true);

            _logger?.LogInformation("Backup restored successfully");

            // Reopen connection
            await context.Database.OpenConnectionAsync(ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to restore from backup");
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
        public int? Rating { get; set; }
        public DateTime? DateAdded { get; set; }
        public DateTime? DateStarted { get; set; }
        public DateTime? DateCompleted { get; set; }
        public string? Genres { get; set; }
    }
}
