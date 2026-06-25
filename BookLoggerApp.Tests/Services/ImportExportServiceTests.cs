using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
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

        public Task UpdateEntitlementMirrorAsync(BookLoggerApp.Core.Entitlements.SubscriptionTier tier, DateTime? expiresAt, CancellationToken ct = default)
            => Task.CompletedTask;

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
    public async Task DeleteAllDataAsync_ResetsProgressionThroughTheSerializedProvider()
    {
        // BUG-08: reset must route through gated UpdateSettingsAsync, not a raw write that can race a coin/XP award.
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContext.Create(dbName);
        var contextFactory = new TestDbContextFactory(dbName);

        var provider = Substitute.For<IAppSettingsProvider>();
        provider.GetSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(new AppSettings { Coins = 999, TotalXp = 777, UserLevel = 9, PlantsPurchased = 5 });

        var service = new ImportExportService(contextFactory, CreateFileSystem(), provider);

        await service.DeleteAllDataAsync();

        await provider.Received(1).UpdateSettingsAsync(
            Arg.Is<AppSettings>(s => s.Coins == 100 && s.TotalXp == 0 && s.UserLevel == 1 && s.PlantsPurchased == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportFromJsonAsync_WithoutWishlistEntitlement_StripsWishlistInfo()
    {
        // HIGH-1003: import must not reintroduce Plus-only Wishlist metadata for an unentitled user.
        var sourceDbName = Guid.NewGuid().ToString();
        using (var sourceContext = TestDbContext.Create(sourceDbName))
        {
            sourceContext.Books.Add(new Book
            {
                Id = Guid.NewGuid(),
                Title = "Wished Book",
                Author = "Author",
                Status = ReadingStatus.Wishlist,
                WishlistInfo = new WishlistInfo { Priority = WishlistPriority.High, WishlistNotes = "want it" }
            });
            await sourceContext.SaveChangesAsync();
        }
        var json = await new ImportExportService(new TestDbContextFactory(sourceDbName), CreateFileSystem(), CreateMockSettingsProvider())
            .ExportToJsonAsync();

        var importDbName = Guid.NewGuid().ToString();
        using var importContext = TestDbContext.Create(importDbName);
        var entitlement = Substitute.For<IEntitlementService>();
        entitlement.HasAccessAsync(FeatureKey.Wishlist, Arg.Any<CancellationToken>()).Returns(false);
        var importService = new ImportExportService(
            new TestDbContextFactory(importDbName), CreateFileSystem(), CreateMockSettingsProvider(), null, null, entitlement);

        var count = await importService.ImportFromJsonAsync(json);

        count.Should().Be(1);
        using var verify = TestDbContext.Create(importDbName);
        var imported = await verify.Books.Include(b => b.WishlistInfo).SingleAsync(b => b.Title == "Wished Book");
        imported.WishlistInfo.Should().BeNull("a Free user is not entitled to Wishlist metadata");
    }

    [Fact]
    public async Task ImportFromJsonAsync_WithWishlistEntitlement_KeepsWishlistInfo()
    {
        var sourceDbName = Guid.NewGuid().ToString();
        using (var sourceContext = TestDbContext.Create(sourceDbName))
        {
            sourceContext.Books.Add(new Book
            {
                Id = Guid.NewGuid(),
                Title = "Wished Book",
                Author = "Author",
                Status = ReadingStatus.Wishlist,
                WishlistInfo = new WishlistInfo { Priority = WishlistPriority.High, WishlistNotes = "want it" }
            });
            await sourceContext.SaveChangesAsync();
        }
        var json = await new ImportExportService(new TestDbContextFactory(sourceDbName), CreateFileSystem(), CreateMockSettingsProvider())
            .ExportToJsonAsync();

        var importDbName = Guid.NewGuid().ToString();
        using var importContext = TestDbContext.Create(importDbName);
        var entitlement = Substitute.For<IEntitlementService>();
        entitlement.HasAccessAsync(FeatureKey.Wishlist, Arg.Any<CancellationToken>()).Returns(true);
        var importService = new ImportExportService(
            new TestDbContextFactory(importDbName), CreateFileSystem(), CreateMockSettingsProvider(), null, null, entitlement);

        await importService.ImportFromJsonAsync(json);

        using var verify = TestDbContext.Create(importDbName);
        var imported = await verify.Books.Include(b => b.WishlistInfo).SingleAsync(b => b.Title == "Wished Book");
        imported.WishlistInfo.Should().NotBeNull("a Plus user keeps imported Wishlist metadata");
        imported.WishlistInfo!.WishlistNotes.Should().Be("want it");
    }

    [Fact]
    public async Task ExportToJsonAsync_ShouldExportBooksAsJson()
    {
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

        var json = await service.ExportToJsonAsync();

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("Test Book");
        json.Should().Contain("Test Author");
        json.Should().Contain("1234567890");
    }

    [Fact]
    public async Task ExportToCsvAsync_ShouldExportBooksAsCsv()
    {
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

        var csv = await service.ExportToCsvAsync();

        csv.Should().NotBeNullOrEmpty();
        csv.Should().Contain("Test Book");
        csv.Should().Contain("Test Author");
        csv.Should().Contain("1234567890");
        csv.Should().Contain("Title"); // CSV header
    }

    [Fact]
    public async Task ExportToCsvAsync_NeutralizesFormulaInjection()
    {
        // Untrusted fields begin with spreadsheet formula triggers.
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContext.Create(dbName);
        context.Books.Add(new Book
        {
            Title = "=HYPERLINK(\"http://evil\",\"x\")",
            Author = "@SUM(1+1)",
            ISBN = "1234567890"
        });
        await context.SaveChangesAsync();

        var contextFactory = new TestDbContextFactory(dbName);
        var service = new ImportExportService(contextFactory, CreateFileSystem(), CreateMockSettingsProvider());

        var csv = await service.ExportToCsvAsync();

        // Dangerous leading chars get an apostrophe prefix so spreadsheets treat them as text.
        csv.Should().Contain("'=HYPERLINK");
        csv.Should().Contain("'@SUM");
        csv.Should().NotContain(",=HYPERLINK");
    }

    [Fact]
    public async Task ImportFromJsonAsync_ShouldImportBooks()
    {
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

        var importedCount = await importService.ImportFromJsonAsync(json);

        importedCount.Should().Be(1);
        // Query from a new context to see the imported data.
        using var verifyContext = TestDbContext.Create(importDbName);
        var books = verifyContext.Books.ToList();
        books.Should().HaveCount(1);
        books[0].Title.Should().Be("Test Book");
        books[0].Author.Should().Be("Test Author");
    }

    [Fact]
    public async Task JsonRoundTrip_ShouldPreserveSessionMoods()
    {
        var exportDbName = Guid.NewGuid().ToString();
        var bookId = Guid.NewGuid();
        using var exportContext = TestDbContext.Create(exportDbName);
        exportContext.Books.Add(new Book
        {
            Id = bookId,
            Title = "Mood Book",
            Author = "Mood Author",
            ISBN = "9990001112",
            ReadingSessions = new List<ReadingSession>
            {
                new()
                {
                    BookId = bookId,
                    Minutes = 25,
                    Moods = new List<ReadingSessionMood>
                    {
                        new() { Mood = SessionMood.Crying },
                        new() { Mood = SessionMood.MindBlown }
                    }
                }
            }
        });
        await exportContext.SaveChangesAsync();

        var exportService = new ImportExportService(
            new TestDbContextFactory(exportDbName), CreateFileSystem(), CreateMockSettingsProvider());
        var json = await exportService.ExportToJsonAsync();

        var importDbName = Guid.NewGuid().ToString();
        using var importContext = TestDbContext.Create(importDbName);
        var importService = new ImportExportService(
            new TestDbContextFactory(importDbName), CreateFileSystem(), CreateMockSettingsProvider());

        var importedCount = await importService.ImportFromJsonAsync(json);

        importedCount.Should().Be(1);
        using var verifyContext = TestDbContext.Create(importDbName);
        var moods = verifyContext.ReadingSessionMoods.Select(m => m.Mood).ToList();
        moods.Should().BeEquivalentTo(new[] { SessionMood.Crying, SessionMood.MindBlown });
    }

    [Fact]
    public async Task ImportFromJsonAsync_WithSeededGenre_DoesNotCollideOnGenrePrimaryKey()
    {
        // BUG-03: exported books carry the seed Genre's fixed Guid; re-importing it used to cause a
        // PK/UNIQUE violation on real SQLite (the EF InMemory provider hides it).
        using var source = new SqliteTestContext();
        Guid seededGenreId;
        string seededGenreName;
        using (var ctx = source.CreateContext())
        {
            var genre = ctx.Genres.First();
            seededGenreId = genre.Id;
            seededGenreName = genre.Name;
            ctx.Books.Add(new Book
            {
                Title = "Imported Book",
                Author = "Author",
                ISBN = "111",
                BookGenres = new List<BookGenre> { new() { GenreId = genre.Id } }
            });
            await ctx.SaveChangesAsync();
        }

        var exportService = new ImportExportService(source.CreateFactory(), CreateFileSystem(), CreateMockSettingsProvider());
        var json = await exportService.ExportToJsonAsync();

        using var target = new SqliteTestContext(); // fresh DB with the same seeded genres
        var importService = new ImportExportService(target.CreateFactory(), CreateFileSystem(), CreateMockSettingsProvider());

        // Must not throw a PK/unique violation on the seeded genre.
        var imported = await importService.ImportFromJsonAsync(json);

        // Book links to the existing seeded genre (find-or-create), no duplicate row.
        imported.Should().Be(1);
        using var verify = target.CreateContext();
        var book = verify.Books.Include(b => b.BookGenres).Single(b => b.Title == "Imported Book");
        book.BookGenres.Should().ContainSingle().Which.GenreId.Should().Be(seededGenreId);
        verify.Genres.Count(g => g.Name == seededGenreName).Should().Be(1);
    }

    [Fact]
    public async Task ImportFromJsonAsync_OnRealEngine_AssignsFreshIdsAndPreservesChildren()
    {
        // BUG-03: import reassigns fresh PKs to book and children to avoid re-import collisions;
        // children (sessions) must survive the id rewiring.
        using var source = new SqliteTestContext();
        Guid originalBookId = Guid.NewGuid();
        using (var ctx = source.CreateContext())
        {
            ctx.Books.Add(new Book
            {
                Id = originalBookId,
                Title = "Child Book",
                Author = "Author",
                ISBN = "222",
                ReadingSessions = new List<ReadingSession>
                {
                    new() { BookId = originalBookId, Minutes = 30 }
                }
            });
            await ctx.SaveChangesAsync();
        }

        var exportService = new ImportExportService(source.CreateFactory(), CreateFileSystem(), CreateMockSettingsProvider());
        var json = await exportService.ExportToJsonAsync();

        using var target = new SqliteTestContext();
        var importService = new ImportExportService(target.CreateFactory(), CreateFileSystem(), CreateMockSettingsProvider());

        var imported = await importService.ImportFromJsonAsync(json);

        imported.Should().Be(1);
        using var verify = target.CreateContext();
        var book = verify.Books.Include(b => b.ReadingSessions).Single(b => b.Title == "Child Book");
        book.Id.Should().NotBe(originalBookId, "the import must assign a fresh primary key");
        book.ReadingSessions.Should().ContainSingle().Which.Minutes.Should().Be(30);
        book.ReadingSessions.Single().BookId.Should().Be(book.Id);
    }

    [Fact]
    public async Task ImportFromCsvAsync_ShouldImportBooks()
    {
        var csv = @"Id,Title,Author,ISBN,Publisher,PublicationYear,Language,Description,PageCount,CurrentPage,CoverImagePath,Status,Rating,DateAdded,DateStarted,DateCompleted,Genres
d5e6f7a8-b9c0-1234-5678-90abcdef1234,Test Book,Test Author,1234567890,Test Publisher,2023,en,Test Description,300,0,,Planned,5,2023-01-01T00:00:00,,,Fiction;Fantasy";

        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContext.Create(dbName);
        var contextFactory = new TestDbContextFactory(dbName);
        var service = new ImportExportService(contextFactory, CreateFileSystem(), CreateMockSettingsProvider());

        var importedCount = await service.ImportFromCsvAsync(csv);

        importedCount.Should().Be(1);
        // Query from a new context to see the imported data.
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
        // Regression: EF's client-side Guid generator populates a new Genre's PK on Add(), so
        // BookGenre.GenreId gets a real Guid before SaveChanges. Three books, five genres, overlapping links.
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

        await service.ImportFromCsvAsync(csv);

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
        // Companion to the multi-book test: two new genres on one book persist as Genre rows
        // and BookGenre junctions with real, distinct PKs.
        const string genreA = "ImportTestGenre_Alpha";
        const string genreB = "ImportTestGenre_Beta";
        var csv = $@"Id,Title,Author,ISBN,Publisher,PublicationYear,Language,Description,PageCount,CurrentPage,CoverImagePath,Status,Rating,DateAdded,DateStarted,DateCompleted,Genres
d5e6f7a8-b9c0-1234-5678-90abcdef1234,Test Book,Test Author,1234567890,Test Publisher,2023,en,Test Description,300,0,,Planned,5,2023-01-01T00:00:00,,,{genreA};{genreB}";

        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContext.Create(dbName);
        var contextFactory = new TestDbContextFactory(dbName);
        var service = new ImportExportService(contextFactory, CreateFileSystem(), CreateMockSettingsProvider());

        await service.ImportFromCsvAsync(csv);

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

        var importedCount = await service.ImportFromJsonAsync(json);

        importedCount.Should().Be(0);
        using var verifyContext = TestDbContext.Create(dbName);
        verifyContext.Books.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteAllDataAsync_WithOwnedDecorations_RemovesDecorationsAndShelves()
    {
        // Regression: DeleteAllDataAsync omitted UserDecorations/DecorationShelves from RemoveRange.
        // UserDecoration -> ShopItem is Restrict, so removing ShopItems threw an FK violation on real
        // SQLite once a decoration was purchased (in-memory left zombie rows instead).
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

        // Must not throw.
        await service.DeleteAllDataAsync();

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

        await service.DeleteAllDataAsync();

        using var verifyContext = TestDbContext.Create(dbName);
        verifyContext.Books.Should().BeEmpty();
        verifyContext.UserDecorations.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateBackupAsync_ShouldCreateBackupFile()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContext.Create(dbName);
        context.Books.Add(new Book { Title = "Test", Author = "Test" });
        await context.SaveChangesAsync();

        var contextFactory = new TestDbContextFactory(dbName);
        var service = new ImportExportService(contextFactory, CreateFileSystem(), CreateMockSettingsProvider());

        // Backup needs a real SQLite file; in-memory DB has no file path so this throws.
        Func<Task> act = async () => await service.CreateBackupAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
