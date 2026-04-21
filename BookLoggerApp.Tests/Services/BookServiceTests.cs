using FluentAssertions;
using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class BookServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MockProgressionService _progressionService;
    private readonly MockPlantService _plantService;
    private readonly MockGoalService _goalService;
    private readonly BookService _service;

    public BookServiceTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);

        _progressionService = new MockProgressionService();
        _plantService = new MockPlantService();
        _goalService = new MockGoalService();
        _service = new BookService(_unitOfWork, _progressionService, _plantService, _goalService, null!);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task AddAsync_ShouldSetDateAdded()
    {
        // Arrange
        var book = new Book { Title = "Test Book", Author = "Test Author" };

        // Act
        var result = await _service.AddAsync(book);

        // Assert
        result.DateAdded.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StartReadingAsync_ShouldUpdateStatusAndDate()
    {
        // Arrange
        var book = await _service.AddAsync(new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            Status = ReadingStatus.Planned
        });

        // Act
        await _service.StartReadingAsync(book.Id);

        // Assert
        var updated = await _service.GetByIdAsync(book.Id);
        updated!.Status.Should().Be(ReadingStatus.Reading);
        updated.DateStarted.Should().NotBeNull();
        updated.DateStarted.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StartReadingAsync_CalledTwice_ShouldNotChangeDateStarted()
    {
        // Arrange
        var book = await _service.AddAsync(new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            Status = ReadingStatus.Planned
        });

        await _service.StartReadingAsync(book.Id);
        var firstStart = (await _service.GetByIdAsync(book.Id))!.DateStarted;

        // Act
        await Task.Delay(25);
        await _service.StartReadingAsync(book.Id);

        // Assert
        var updated = await _service.GetByIdAsync(book.Id);
        updated!.Status.Should().Be(ReadingStatus.Reading);
        updated.DateStarted.Should().Be(firstStart);
    }

    [Fact]
    public async Task CompleteBookAsync_ShouldUpdateStatusAndDate()
    {
        // Arrange
        var book = await _service.AddAsync(new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            Status = ReadingStatus.Reading,
            PageCount = 100,
            CurrentPage = 95
        });

        // Act
        await _service.CompleteBookAsync(book.Id);

        // Assert
        var updated = await _service.GetByIdAsync(book.Id);
        updated!.Status.Should().Be(ReadingStatus.Completed);
        updated.DateCompleted.Should().NotBeNull();
        updated.CurrentPage.Should().Be(100); // Should set to PageCount
        _goalService.RecalculateGoalProgressCallCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateProgressAsync_ShouldAutoCompleteWhenLastPage()
    {
        // Arrange
        var book = await _service.AddAsync(new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            PageCount = 100,
            CurrentPage = 95,
            Status = ReadingStatus.Reading
        });

        // Act
        var result = await _service.UpdateProgressAsync(book.Id, 100);

        // Assert
        result.Should().NotBeNull("auto-completion should return a ProgressionResult");
        result!.BookCompletionXp.Should().BeGreaterThanOrEqualTo(0);

        var updated = await _service.GetByIdAsync(book.Id);
        updated!.Status.Should().Be(ReadingStatus.Completed);
        updated.DateCompleted.Should().NotBeNull();
        updated.CurrentPage.Should().Be(100);
        _goalService.RecalculateGoalProgressCallCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateProgressAsync_ShouldReturnNull_WhenNotCompleting()
    {
        // Arrange
        var book = await _service.AddAsync(new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            PageCount = 100,
            CurrentPage = 50,
            Status = ReadingStatus.Reading
        });

        // Act
        var result = await _service.UpdateProgressAsync(book.Id, 75);

        // Assert
        result.Should().BeNull("no auto-completion should return null");

        var updated = await _service.GetByIdAsync(book.Id);
        updated!.Status.Should().Be(ReadingStatus.Reading);
        updated.CurrentPage.Should().Be(75);
    }

    [Fact]
    public async Task CompleteBookAsync_CalledTwice_AwardsCompletionXpOnce()
    {
        // Arrange
        var book = await _service.AddAsync(new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            Status = ReadingStatus.Reading,
            PageCount = 100,
            CurrentPage = 95
        });

        // Act
        await _service.CompleteBookAsync(book.Id);
        await _service.CompleteBookAsync(book.Id);

        // Assert
        _progressionService.AwardBookCompletionXpCallCount.Should().Be(1,
            "a second completion of the same book must not re-award XP");
        _goalService.RecalculateGoalProgressCallCount.Should().Be(1,
            "goal-recalc only runs on the first completion transition");
    }

    [Fact]
    public async Task CompleteBookAsync_PreservesOriginalDateCompleted()
    {
        // Arrange
        var originalCompletion = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var book = await _service.AddAsync(new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            Status = ReadingStatus.Completed,
            PageCount = 100,
            CurrentPage = 100,
            DateCompleted = originalCompletion
        });

        // Act
        await _service.CompleteBookAsync(book.Id);

        // Assert
        var updated = await _service.GetByIdAsync(book.Id);
        updated!.DateCompleted.Should().Be(originalCompletion,
            "DateCompleted must not be overwritten when the book is already completed");
    }

    [Fact]
    public async Task UpdateProgressAsync_ReachingLastPageAgain_DoesNotAwardXpTwice()
    {
        // Arrange
        var originalCompletion = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var book = await _service.AddAsync(new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            Status = ReadingStatus.Completed,
            PageCount = 100,
            CurrentPage = 100,
            DateCompleted = originalCompletion
        });
        // Reset the counter — we want to measure only subsequent calls.
        var initialCount = _progressionService.AwardBookCompletionXpCallCount;

        // Act: user scrubs progress back below PageCount and then to PageCount again.
        await _service.UpdateProgressAsync(book.Id, 80);
        var resultSecondCompletion = await _service.UpdateProgressAsync(book.Id, 100);

        // Assert
        resultSecondCompletion.Should().BeNull(
            "UpdateProgressAsync must only return a ProgressionResult on the first completion");
        _progressionService.AwardBookCompletionXpCallCount.Should().Be(initialCount,
            "re-reaching the last page must not re-award completion XP");

        var updated = await _service.GetByIdAsync(book.Id);
        updated!.DateCompleted.Should().Be(originalCompletion,
            "DateCompleted must not be overwritten by re-completion");
        updated.Status.Should().Be(ReadingStatus.Completed);
        updated.CurrentPage.Should().Be(100);
    }

    [Fact]
    public async Task UpdateProgressAsync_FirstCompletion_AwardsXpAndSetsDate()
    {
        // Arrange
        var book = await _service.AddAsync(new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            Status = ReadingStatus.Reading,
            PageCount = 100,
            CurrentPage = 99
        });
        var initialCount = _progressionService.AwardBookCompletionXpCallCount;

        // Act
        var result = await _service.UpdateProgressAsync(book.Id, 100);

        // Assert
        result.Should().NotBeNull("first completion must return a ProgressionResult");
        _progressionService.AwardBookCompletionXpCallCount.Should().Be(initialCount + 1);

        var updated = await _service.GetByIdAsync(book.Id);
        updated!.Status.Should().Be(ReadingStatus.Completed);
        updated.DateCompleted.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAsync_ShouldFindBooksByTitleOrAuthor()
    {
        // Arrange
        await _service.AddAsync(new Book { Title = "The Hobbit", Author = "J.R.R. Tolkien" });
        await _service.AddAsync(new Book { Title = "1984", Author = "George Orwell" });
        await _service.AddAsync(new Book { Title = "The Lord of the Rings", Author = "J.R.R. Tolkien" });

        // Act
        var results = await _service.SearchAsync("Tolkien");

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(b => b.Author.Contains("Tolkien"));
    }

    [Fact]
    public async Task GetByStatusAsync_ShouldReturnOnlyBooksWithStatus()
    {
        // Arrange
        await _service.AddAsync(new Book { Title = "Book 1", Status = ReadingStatus.Reading });
        await _service.AddAsync(new Book { Title = "Book 2", Status = ReadingStatus.Planned });
        await _service.AddAsync(new Book { Title = "Book 3", Status = ReadingStatus.Completed });

        // Act
        var readingBooks = await _service.GetByStatusAsync(ReadingStatus.Reading);

        // Assert
        readingBooks.Should().HaveCount(1);
        readingBooks.First().Title.Should().Be("Book 1");
    }

    [Fact]
    public async Task ImportBooksAsync_ShouldImportMultipleBooks()
    {
        // Arrange
        var books = new[]
        {
            new Book { Title = "Book 1", Author = "Author 1" },
            new Book { Title = "Book 2", Author = "Author 2" },
            new Book { Title = "Book 3", Author = "Author 3" }
        };

        // Act
        var count = await _service.ImportBooksAsync(books);

        // Assert
        count.Should().Be(3);
        var allBooks = await _service.GetAllAsync();
        allBooks.Should().HaveCount(3);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Coverage-ergänzende Tests (GetByIdAsync null, GetAllAsync-Order,
    // DeleteAsync, GetByISBN, GetByGenre, GetWithDetails, Count, Update etc.)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_NotExisting_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_OrdersByDateAddedDescending()
    {
        await _service.AddAsync(new Book { Title = "Old", Author = "a", DateAdded = DateTime.UtcNow.AddDays(-3) });
        await _service.AddAsync(new Book { Title = "Newest", Author = "a", DateAdded = DateTime.UtcNow });
        await _service.AddAsync(new Book { Title = "Middle", Author = "a", DateAdded = DateTime.UtcNow.AddDays(-1) });

        var all = await _service.GetAllAsync();

        all[0].Title.Should().Be("Newest");
        all[1].Title.Should().Be("Middle");
        all[2].Title.Should().Be("Old");
    }

    [Fact]
    public async Task AddAsync_PreservesProvidedDateAdded()
    {
        var preset = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var book = new Book { Title = "Preset", Author = "a", DateAdded = preset };

        var result = await _service.AddAsync(book);

        result.DateAdded.Should().Be(preset);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var book = await _service.AddAsync(new Book { Title = "Orig", Author = "a" });
        book.Title = "Changed";

        await _service.UpdateAsync(book);

        var reloaded = await _service.GetByIdAsync(book.Id);
        reloaded!.Title.Should().Be("Changed");
    }

    [Fact]
    public async Task DeleteAsync_Existing_RemovesBook()
    {
        var book = await _service.AddAsync(new Book { Title = "Del", Author = "a" });

        await _service.DeleteAsync(book.Id);

        (await _service.GetByIdAsync(book.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NotExisting_IsNoOp()
    {
        Func<Task> act = async () => await _service.DeleteAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetByISBNAsync_Existing_ReturnsBook()
    {
        await _service.AddAsync(new Book { Title = "X", Author = "a", ISBN = "9781234567890" });

        var result = await _service.GetByISBNAsync("9781234567890");

        result.Should().NotBeNull();
        result!.ISBN.Should().Be("9781234567890");
    }

    [Fact]
    public async Task GetByISBNAsync_NotExisting_ReturnsNull()
    {
        var result = await _service.GetByISBNAsync("0000000000");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByGenreAsync_ReturnsBooksWithGenre()
    {
        var book = await _service.AddAsync(new Book { Title = "GenreBook", Author = "a" });
        var genre = new Genre { Id = Guid.NewGuid(), Name = "SF" };
        _context.Genres.Add(genre);
        _context.BookGenres.Add(new BookGenre { BookId = book.Id, GenreId = genre.Id });
        await _context.SaveChangesAsync();

        var result = await _service.GetByGenreAsync(genre.Id);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("GenreBook");
    }

    [Fact]
    public async Task GetWithDetailsAsync_IncludesRelatedEntities()
    {
        var book = await _service.AddAsync(new Book { Title = "Detailed", Author = "a" });
        _context.Quotes.Add(new Quote { BookId = book.Id, Text = "Q" });
        _context.Annotations.Add(new Annotation { BookId = book.Id, Note = "A" });
        await _context.SaveChangesAsync();

        var result = await _service.GetWithDetailsAsync(book.Id);

        result.Should().NotBeNull();
        result!.Quotes.Should().HaveCount(1);
        result.Annotations.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTotalCountAsync_ReturnsTotal()
    {
        await _service.AddAsync(new Book { Title = "a", Author = "x" });
        await _service.AddAsync(new Book { Title = "b", Author = "x" });
        await _service.AddAsync(new Book { Title = "c", Author = "x" });

        var count = await _service.GetTotalCountAsync();

        count.Should().Be(3);
    }

    [Fact]
    public async Task GetCountByStatusAsync_CountsPerStatus()
    {
        await _service.AddAsync(new Book { Title = "a", Author = "x", Status = ReadingStatus.Reading });
        await _service.AddAsync(new Book { Title = "b", Author = "x", Status = ReadingStatus.Reading });
        await _service.AddAsync(new Book { Title = "c", Author = "x", Status = ReadingStatus.Completed });

        (await _service.GetCountByStatusAsync(ReadingStatus.Reading)).Should().Be(2);
        (await _service.GetCountByStatusAsync(ReadingStatus.Completed)).Should().Be(1);
        (await _service.GetCountByStatusAsync(ReadingStatus.Planned)).Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsAll()
    {
        await _service.AddAsync(new Book { Title = "a", Author = "x" });
        await _service.AddAsync(new Book { Title = "b", Author = "x" });

        var result = await _service.SearchAsync("");

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_WhitespaceQuery_ReturnsAll()
    {
        await _service.AddAsync(new Book { Title = "a", Author = "x" });

        var result = await _service.SearchAsync("   ");

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task StartReadingAsync_NonExisting_ThrowsEntityNotFound()
    {
        Func<Task> act = async () => await _service.StartReadingAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task CompleteBookAsync_NonExisting_ThrowsEntityNotFound()
    {
        Func<Task> act = async () => await _service.CompleteBookAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task UpdateProgressAsync_NonExisting_ThrowsEntityNotFound()
    {
        Func<Task> act = async () => await _service.UpdateProgressAsync(Guid.NewGuid(), 50);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task UpdateProgressAsync_ClampsAboveMaxPages()
    {
        var book = await _service.AddAsync(new Book
        {
            Title = "Clamp",
            Author = "a",
            PageCount = 100,
            CurrentPage = 50,
            Status = ReadingStatus.Reading
        });

        await _service.UpdateProgressAsync(book.Id, 600);

        var reloaded = await _service.GetByIdAsync(book.Id);
        reloaded!.CurrentPage.Should().Be(100);
        reloaded.Status.Should().Be(ReadingStatus.Completed);
    }

    [Fact]
    public async Task UpdateProgressAsync_ClampsNegative()
    {
        var book = await _service.AddAsync(new Book
        {
            Title = "NegClamp",
            Author = "a",
            PageCount = 100,
            CurrentPage = 50,
            Status = ReadingStatus.Reading
        });

        await _service.UpdateProgressAsync(book.Id, -5);

        var reloaded = await _service.GetByIdAsync(book.Id);
        reloaded!.CurrentPage.Should().Be(0);
        reloaded.Status.Should().Be(ReadingStatus.Reading);
    }

    [Fact]
    public async Task UpdateProgressAsync_NoPageCount_DoesNotCompleteOnHighPage()
    {
        var book = await _service.AddAsync(new Book
        {
            Title = "NoPages",
            Author = "a",
            PageCount = null,
            CurrentPage = 50,
            Status = ReadingStatus.Reading
        });

        var result = await _service.UpdateProgressAsync(book.Id, 10000);

        result.Should().BeNull();
        var reloaded = await _service.GetByIdAsync(book.Id);
        reloaded!.Status.Should().Be(ReadingStatus.Reading);
        reloaded.CurrentPage.Should().Be(10000);
    }
}
