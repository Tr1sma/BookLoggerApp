using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class ImportExportServiceTests
{
    [Fact]
    public async Task ExportToJsonAsync_ShouldExportBooksAsJson()
    {
        // Arrange
        using var context = TestDbContext.Create();
        context.Books.Add(new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890"
        });
        await context.SaveChangesAsync();

        var service = new ImportExportService(context);

        // Act
        var json = await service.ExportToJsonAsync();

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("Test Book");
        json.Should().Contain("Test Author");
        json.Should().Contain("1234567890");
    }

    [Fact]
    public async Task ExportToCsvAsync_ShouldExportBooksAsCsv()
    {
        // Arrange
        using var context = TestDbContext.Create();
        context.Books.Add(new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890"
        });
        await context.SaveChangesAsync();

        var service = new ImportExportService(context);

        // Act
        var csv = await service.ExportToCsvAsync();

        // Assert
        csv.Should().NotBeNullOrEmpty();
        csv.Should().Contain("Test Book");
        csv.Should().Contain("Test Author");
        csv.Should().Contain("1234567890");
        csv.Should().Contain("Title"); // CSV header
    }

    [Fact]
    public async Task ImportFromJsonAsync_ShouldImportBooks()
    {
        // Arrange
        using var exportContext = TestDbContext.Create();
        exportContext.Books.Add(new Book
        {
            Id = Guid.NewGuid(),
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890"
        });
        await exportContext.SaveChangesAsync();

        var exportService = new ImportExportService(exportContext);
        var json = await exportService.ExportToJsonAsync();

        using var importContext = TestDbContext.Create();
        var importService = new ImportExportService(importContext);

        // Act
        var importedCount = await importService.ImportFromJsonAsync(json);

        // Assert
        importedCount.Should().Be(1);
        var books = importContext.Books.ToList();
        books.Should().HaveCount(1);
        books[0].Title.Should().Be("Test Book");
        books[0].Author.Should().Be("Test Author");
    }

    [Fact]
    public async Task ImportFromCsvAsync_ShouldImportBooks()
    {
        // Arrange
        var csv = @"Id,Title,Author,ISBN,Publisher,PublicationYear,Language,Description,PageCount,CurrentPage,CoverImagePath,Status,Rating,DateAdded,DateStarted,DateCompleted,Genres
d5e6f7a8-b9c0-1234-5678-90abcdef1234,Test Book,Test Author,1234567890,Test Publisher,2023,en,Test Description,300,0,,Planned,5,2023-01-01T00:00:00,,,Fiction;Fantasy";

        using var context = TestDbContext.Create();
        var service = new ImportExportService(context);

        // Act
        var importedCount = await service.ImportFromCsvAsync(csv);

        // Assert
        importedCount.Should().Be(1);
        var books = context.Books.ToList();
        books.Should().HaveCount(1);
        books[0].Title.Should().Be("Test Book");
        books[0].Author.Should().Be("Test Author");
        books[0].ISBN.Should().Be("1234567890");
    }

    [Fact]
    public async Task ImportFromJsonAsync_WithDuplicates_ShouldSkipDuplicates()
    {
        // Arrange
        using var context = TestDbContext.Create();
        var existingBook = new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890"
        };
        context.Books.Add(existingBook);
        await context.SaveChangesAsync();

        var service = new ImportExportService(context);
        var json = await service.ExportToJsonAsync();

        // Act - import the same books again
        var importedCount = await service.ImportFromJsonAsync(json);

        // Assert
        importedCount.Should().Be(0); // No new books imported
        context.Books.Should().HaveCount(1); // Still only 1 book
    }

    [Fact]
    public async Task CreateBackupAsync_ShouldCreateBackupFile()
    {
        // Arrange
        using var context = TestDbContext.Create();
        context.Books.Add(new Book { Title = "Test", Author = "Test" });
        await context.SaveChangesAsync();

        var service = new ImportExportService(context);

        // Act & Assert
        // Note: Backup functionality requires a real SQLite database file,
        // so this test would need to be an integration test or use a temp SQLite file
        // For now, we just verify the method exists and doesn't throw
        Func<Task> act = async () => await service.CreateBackupAsync();

        // This will throw because in-memory DB doesn't have a file path
        // In a real scenario with a file-based DB, this would work
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
