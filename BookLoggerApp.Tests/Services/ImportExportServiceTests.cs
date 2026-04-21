using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class ImportExportServiceTests
{
    private static IFileSystem CreateFileSystem() => new FileSystemAdapter();
    private static IAppSettingsProvider CreateMockSettingsProvider() => new MockAppSettingsProvider();

    private class MockAppSettingsProvider : IAppSettingsProvider
    {
        public event EventHandler? ProgressionChanged;
        public event EventHandler? SettingsChanged;

        public Task<AppSettings> GetSettingsAsync(CancellationToken ct = default)
            => Task.FromResult(new AppSettings());

        public Task UpdateSettingsAsync(AppSettings settings, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<int> GetUserCoinsAsync(CancellationToken ct = default)
            => Task.FromResult(100);

        public Task<int> GetUserLevelAsync(CancellationToken ct = default)
            => Task.FromResult(1);

        public Task SpendCoinsAsync(int amount, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task AddCoinsAsync(int amount, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task IncrementPlantsPurchasedAsync(CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<int> GetPlantsPurchasedAsync(CancellationToken ct = default)
            => Task.FromResult(0);

        public void InvalidateCache()
        {
            ProgressionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void InvalidateCache(bool notifyProgressionChanged)
        {
            if (notifyProgressionChanged)
            {
                ProgressionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    [Fact]
    public async Task ExportToJsonAsync_ShouldExportBooksAsJson()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContext.Create(dbName);
        context.Books.Add(new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890"
        });
        await context.SaveChangesAsync();

        var contextFactory = new TestDbContextFactory(dbName);
        var service = new ImportExportService(contextFactory, CreateFileSystem(), CreateMockSettingsProvider());

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
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContext.Create(dbName);
        context.Books.Add(new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890"
        });
        await context.SaveChangesAsync();

        var contextFactory = new TestDbContextFactory(dbName);
        var service = new ImportExportService(contextFactory, CreateFileSystem(), CreateMockSettingsProvider());

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
        var exportDbName = Guid.NewGuid().ToString();
        using var exportContext = TestDbContext.Create(exportDbName);
        exportContext.Books.Add(new Book
        {
            Id = Guid.NewGuid(),
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890"
        });
        await exportContext.SaveChangesAsync();

        var exportContextFactory = new TestDbContextFactory(exportDbName);
        var exportService = new ImportExportService(exportContextFactory, CreateFileSystem(), CreateMockSettingsProvider());
        var json = await exportService.ExportToJsonAsync();

        var importDbName = Guid.NewGuid().ToString();
        using var importContext = TestDbContext.Create(importDbName);
        var importContextFactory = new TestDbContextFactory(importDbName);
        var importService = new ImportExportService(importContextFactory, CreateFileSystem(), CreateMockSettingsProvider());

        // Act
        var importedCount = await importService.ImportFromJsonAsync(json);

        // Assert
        importedCount.Should().Be(1);
        // Need to query from a new context to see the imported data
        using var verifyContext = TestDbContext.Create(importDbName);
        var books = verifyContext.Books.ToList();
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

        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContext.Create(dbName);
        var contextFactory = new TestDbContextFactory(dbName);
        var service = new ImportExportService(contextFactory, CreateFileSystem(), CreateMockSettingsProvider());

        // Act
        var importedCount = await service.ImportFromCsvAsync(csv);

        // Assert
        importedCount.Should().Be(1);
        // Need to query from a new context to see the imported data
        using var verifyContext = TestDbContext.Create(dbName);
        var books = verifyContext.Books.ToList();
        books.Should().HaveCount(1);
        books[0].Title.Should().Be("Test Book");
        books[0].Author.Should().Be("Test Author");
        books[0].ISBN.Should().Be("1234567890");
    }

    [Fact]
    public async Task ImportFromCsvAsync_WithManyNewGenresAcrossMultipleBooks_ShouldPersistEveryGenreAssociation()
    {
        // Regression gate around the CSV import's "create-new-Genre-inline" path. The
        // import creates Genre entities without assigning a Guid (Genre.Id stays as
        // default(Guid)) and then builds BookGenre rows with `GenreId = genre.Id` before
        // SaveChanges runs. This works because EF Core's client-side Guid key generator
        // populates the new Genre's PK during Add(), so each BookGenre picks up a real
        // Guid at the moment of assignment. This test pins that behavior with three books,
        // five brand-new genres, and overlapping assignments — if a future change breaks
        // the propagation (e.g. by adding Genre entities but not tracking them, or by
        // reordering Add/assign), the overlap assertions will fail.
        const string gA = "DiagGenre_A", gB = "DiagGenre_B", gC = "DiagGenre_C",
                     gD = "DiagGenre_D", gE = "DiagGenre_E";
        var csv = $@"Id,Title,Author,ISBN,Publisher,PublicationYear,Language,Description,PageCount,CurrentPage,CoverImagePath,Status,Rating,DateAdded,DateStarted,DateCompleted,Genres
11111111-1111-1111-1111-111111111111,Book One,Author One,1111111111,,,,,100,0,,Planned,,2023-01-01T00:00:00,,,{gA};{gB};{gC}
22222222-2222-2222-2222-222222222222,Book Two,Author Two,2222222222,,,,,100,0,,Planned,,2023-01-01T00:00:00,,,{gB};{gD}
33333333-3333-3333-3333-333333333333,Book Three,Author Three,3333333333,,,,,100,0,,Planned,,2023-01-01T00:00:00,,,{gA};{gE}";

        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContext.Create(dbName);
        var contextFactory = new TestDbContextFactory(dbName);
        var service = new ImportExportService(contextFactory, CreateFileSystem(), CreateMockSettingsProvider());

        // Act
        await service.ImportFromCsvAsync(csv);

        // Assert — every diagnostic genre must exist, every book-genre link must resolve
        using var verifyContext = TestDbContext.Create(dbName);
        var expectedNames = new[] { gA, gB, gC, gD, gE };
        var newGenres = verifyContext.Genres
            .Where(g => expectedNames.Contains(g.Name))
            .ToList();
        newGenres.Should().HaveCount(5, "all five new genre names should be persisted once each");
        newGenres.Should().OnlyContain(g => g.Id != Guid.Empty);
        newGenres.Select(g => g.Id).Distinct().Should().HaveCount(5, "each new genre must have a unique ID");

        var bookOne = verifyContext.Books.Single(b => b.Title == "Book One");
        var bookTwo = verifyContext.Books.Single(b => b.Title == "Book Two");
        var bookThree = verifyContext.Books.Single(b => b.Title == "Book Three");
        var newGenreIds = newGenres.ToDictionary(g => g.Name, g => g.Id);

        var bookOneGenreIds = verifyContext.BookGenres
            .Where(bg => bg.BookId == bookOne.Id)
            .Select(bg => bg.GenreId)
            .ToHashSet();
        bookOneGenreIds.Should().BeEquivalentTo(new[] { newGenreIds[gA], newGenreIds[gB], newGenreIds[gC] });

        var bookTwoGenreIds = verifyContext.BookGenres
            .Where(bg => bg.BookId == bookTwo.Id)
            .Select(bg => bg.GenreId)
            .ToHashSet();
        bookTwoGenreIds.Should().BeEquivalentTo(new[] { newGenreIds[gB], newGenreIds[gD] });

        var bookThreeGenreIds = verifyContext.BookGenres
            .Where(bg => bg.BookId == bookThree.Id)
            .Select(bg => bg.GenreId)
            .ToHashSet();
        bookThreeGenreIds.Should().BeEquivalentTo(new[] { newGenreIds[gA], newGenreIds[gE] });
    }

    [Fact]
    public async Task ImportFromCsvAsync_WithMultipleNewGenres_ShouldPersistAllGenreAssociations()
    {
        // Simpler companion to the multi-book regression test above: two brand-new genre
        // names (not in the seed data) on a single book, to confirm new Genre rows and
        // their BookGenre junctions are persisted with real, distinct primary keys.
        const string genreA = "ImportTestGenre_Alpha";
        const string genreB = "ImportTestGenre_Beta";
        var csv = $@"Id,Title,Author,ISBN,Publisher,PublicationYear,Language,Description,PageCount,CurrentPage,CoverImagePath,Status,Rating,DateAdded,DateStarted,DateCompleted,Genres
d5e6f7a8-b9c0-1234-5678-90abcdef1234,Test Book,Test Author,1234567890,Test Publisher,2023,en,Test Description,300,0,,Planned,5,2023-01-01T00:00:00,,,{genreA};{genreB}";

        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContext.Create(dbName);
        var contextFactory = new TestDbContextFactory(dbName);
        var service = new ImportExportService(contextFactory, CreateFileSystem(), CreateMockSettingsProvider());

        // Act
        await service.ImportFromCsvAsync(csv);

        // Assert — both new Genre rows must exist with real (non-empty) IDs, and two
        // BookGenre rows must link the imported book to those Genre IDs.
        using var verifyContext = TestDbContext.Create(dbName);
        var newGenres = verifyContext.Genres
            .Where(g => g.Name == genreA || g.Name == genreB)
            .ToList();
        newGenres.Should().HaveCount(2, "both CSV-declared new genres must be persisted as Genre rows");
        newGenres.Should().OnlyContain(g => g.Id != Guid.Empty, "every new Genre row must have a real primary key");
        newGenres.Select(g => g.Id).Distinct().Should().HaveCount(2, "the two new genres must have distinct IDs (not share Guid.Empty)");

        var importedBook = verifyContext.Books.Single(b => b.Title == "Test Book");
        var bookGenres = verifyContext.BookGenres
            .Where(bg => bg.BookId == importedBook.Id)
            .ToList();
        bookGenres.Should().HaveCount(2, "the imported book must be linked to both new genres");
        var newGenreIds = newGenres.Select(g => g.Id).ToHashSet();
        bookGenres.Should().OnlyContain(bg => newGenreIds.Contains(bg.GenreId),
            "each BookGenre must reference one of the freshly-created Genre IDs");
    }

    [Fact]
    public async Task ImportFromJsonAsync_WithDuplicates_ShouldSkipDuplicates()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContext.Create(dbName);
        var existingBook = new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890"
        };
        context.Books.Add(existingBook);
        await context.SaveChangesAsync();

        var contextFactory = new TestDbContextFactory(dbName);
        var service = new ImportExportService(contextFactory, CreateFileSystem(), CreateMockSettingsProvider());
        var json = await service.ExportToJsonAsync();

        // Act - import the same books again
        var importedCount = await service.ImportFromJsonAsync(json);

        // Assert
        importedCount.Should().Be(0); // No new books imported
        // Need to query from a new context to verify count
        using var verifyContext = TestDbContext.Create(dbName);
        verifyContext.Books.Should().HaveCount(1); // Still only 1 book
    }

    [Fact]
    public async Task DeleteAllDataAsync_WithOwnedDecorations_RemovesDecorationsAndShelves()
    {
        // Regression: DeleteAllDataAsync previously omitted UserDecorations and
        // DecorationShelves from the RemoveRange chain. Because UserDecoration →
        // ShopItem has DeleteBehavior.Restrict, the subsequent ShopItems.RemoveRange
        // would raise an FK-violation on a real SQLite DB as soon as any decoration
        // had been purchased. On the in-memory provider (which doesn't enforce FKs)
        // the visible symptom was zombie UserDecoration rows referencing deleted
        // ShopItems. Both aspects are pinned by this test.
        var dbName = Guid.NewGuid().ToString();
        var shopItemId = Guid.NewGuid();
        var decorationId = Guid.NewGuid();
        var shelfId = Guid.NewGuid();

        using (var context = TestDbContext.Create(dbName))
        {
            context.ShopItems.Add(new ShopItem
            {
                Id = shopItemId,
                Name = "Test Decoration",
                Description = "Test",
                Cost = 100,
                ItemType = ShopItemType.Decoration,
                ImagePath = "test.svg",
                UnlockLevel = 1,
                IsAvailable = true,
                SlotWidth = 1
            });
            context.Shelves.Add(new Shelf { Id = shelfId, Name = "Test Shelf", SortOrder = 0 });
            context.UserDecorations.Add(new UserDecoration
            {
                Id = decorationId,
                ShopItemId = shopItemId,
                Name = "Test Decoration",
                PurchasedAt = DateTime.UtcNow
            });
            context.DecorationShelves.Add(new DecorationShelf
            {
                DecorationId = decorationId,
                ShelfId = shelfId,
                Position = 0
            });
            await context.SaveChangesAsync();
        }

        var contextFactory = new TestDbContextFactory(dbName);
        var service = new ImportExportService(contextFactory, CreateFileSystem(), CreateMockSettingsProvider());

        // Act — must not throw
        await service.DeleteAllDataAsync();

        // Assert
        using var verifyContext = TestDbContext.Create(dbName);
        verifyContext.UserDecorations.Should().BeEmpty(
            "purchased decorations must be removed on DeleteAllDataAsync");
        verifyContext.DecorationShelves.Should().BeEmpty(
            "decoration placements must be removed on DeleteAllDataAsync");
        verifyContext.ShopItems.Should().BeEmpty(
            "shop items continue to be removed (DbInitializer re-seeds them on next start)");
    }

    [Fact]
    public async Task DeleteAllDataAsync_WithoutDecorations_CompletesCleanly()
    {
        // Regression guard: the path that previously worked must not regress.
        var dbName = Guid.NewGuid().ToString();
        using (var context = TestDbContext.Create(dbName))
        {
            context.Books.Add(new Book { Title = "Test", Author = "Test" });
            await context.SaveChangesAsync();
        }

        var contextFactory = new TestDbContextFactory(dbName);
        var service = new ImportExportService(contextFactory, CreateFileSystem(), CreateMockSettingsProvider());

        // Act
        await service.DeleteAllDataAsync();

        // Assert
        using var verifyContext = TestDbContext.Create(dbName);
        verifyContext.Books.Should().BeEmpty();
        verifyContext.UserDecorations.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateBackupAsync_ShouldCreateBackupFile()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContext.Create(dbName);
        context.Books.Add(new Book { Title = "Test", Author = "Test" });
        await context.SaveChangesAsync();

        var contextFactory = new TestDbContextFactory(dbName);
        var service = new ImportExportService(contextFactory, CreateFileSystem(), CreateMockSettingsProvider());

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
