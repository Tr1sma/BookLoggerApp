using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class BookServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly BookRepository _repository;
    private readonly BookService _service;

    public BookServiceTests()
    {
        _context = TestDbContext.Create();
        _repository = new BookRepository(_context);
        _service = new BookService(_repository);
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
        await _service.UpdateProgressAsync(book.Id, 100);

        // Assert
        var updated = await _service.GetByIdAsync(book.Id);
        updated!.Status.Should().Be(ReadingStatus.Completed);
        updated.DateCompleted.Should().NotBeNull();
        updated.CurrentPage.Should().Be(100);
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
}
