using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Repositories;

public class BookRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly BookRepository _repository;

    public BookRepositoryTests()
    {
        _context = TestDbContext.Create();
        _repository = new BookRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task AddAsync_ShouldAddBook()
    {
        // Arrange
        var book = new Book
        {
            Title = "Test Book",
            Author = "Test Author"
        };

        // Act
        var result = await _repository.AddAsync(book);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        var savedBook = await _repository.GetByIdAsync(result.Id);
        savedBook.Should().NotBeNull();
        savedBook!.Title.Should().Be("Test Book");
    }

    [Fact]
    public async Task GetBooksByStatusAsync_ShouldReturnOnlyBooksWithStatus()
    {
        // Arrange
        await _repository.AddAsync(new Book { Title = "Reading Book", Status = ReadingStatus.Reading });
        await _repository.AddAsync(new Book { Title = "Planned Book", Status = ReadingStatus.Planned });
        await _repository.AddAsync(new Book { Title = "Completed Book", Status = ReadingStatus.Completed });
        await _context.SaveChangesAsync();

        // Act
        var readingBooks = await _repository.GetBooksByStatusAsync(ReadingStatus.Reading);

        // Assert
        readingBooks.Should().HaveCount(1);
        readingBooks.First().Title.Should().Be("Reading Book");
    }

    [Fact]
    public async Task SearchBooksAsync_ShouldFindBooksByTitleOrAuthor()
    {
        // Arrange
        await _repository.AddAsync(new Book { Title = "The Hobbit", Author = "J.R.R. Tolkien" });
        await _repository.AddAsync(new Book { Title = "1984", Author = "George Orwell" });
        await _repository.AddAsync(new Book { Title = "The Lord of the Rings", Author = "J.R.R. Tolkien" });
        await _context.SaveChangesAsync();

        // Act
        var tolkienBooks = await _repository.SearchBooksAsync("tolkien");

        // Assert
        tolkienBooks.Should().HaveCount(2);
        tolkienBooks.Should().OnlyContain(b => b.Author.Contains("Tolkien"));
    }

    [Fact]
    public async Task GetBookByISBNAsync_ShouldReturnCorrectBook()
    {
        // Arrange
        var isbn = "9780547928227";
        await _repository.AddAsync(new Book { Title = "The Hobbit", ISBN = isbn });
        await _repository.AddAsync(new Book { Title = "1984", ISBN = "9780451524935" });
        await _context.SaveChangesAsync();

        // Act
        var book = await _repository.GetBookByISBNAsync(isbn);

        // Assert
        book.Should().NotBeNull();
        book!.Title.Should().Be("The Hobbit");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateBook()
    {
        // Arrange
        var book = await _repository.AddAsync(new Book { Title = "Original Title", Author = "Author" });
        await _context.SaveChangesAsync();
        book.Title = "Updated Title";

        // Act
        await _repository.UpdateAsync(book);

        // Assert
        var updatedBook = await _repository.GetByIdAsync(book.Id);
        updatedBook!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveBook()
    {
        // Arrange
        var book = await _repository.AddAsync(new Book { Title = "To Delete", Author = "Author" });
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(book);
        await _context.SaveChangesAsync(); // Must save changes after delete

        // Assert
        var deletedBook = await _repository.GetByIdAsync(book.Id);
        deletedBook.Should().BeNull();
    }

    [Fact]
    public async Task GetRecentBooksAsync_ShouldReturnMostRecentBooks()
    {
        // Arrange
        await _repository.AddAsync(new Book { Title = "Book 1" });
        await Task.Delay(10);
        await _repository.AddAsync(new Book { Title = "Book 2" });
        await Task.Delay(10);
        await _repository.AddAsync(new Book { Title = "Book 3" });
        await _context.SaveChangesAsync();

        // Act
        var recentBooks = await _repository.GetRecentBooksAsync(2);

        // Assert
        recentBooks.Should().HaveCount(2);
        recentBooks.First().Title.Should().Be("Book 3");
    }
    [Fact]
    public async Task SearchBooksAsync_ShouldFindBooksByGenreOrTrope()
    {
        // Arrange
        var fantasyGenre = new Genre { Name = "Fantasy" };
        var sciFiGenre = new Genre { Name = "Sci-Fi" };
        
        var magicTrope = new Trope { Name = "Magic" };
        var spaceTrope = new Trope { Name = "Space Opera" };

        var book1 = new Book { Title = "Book 1", Author = "Author A" };
        book1.BookGenres.Add(new BookGenre { Book = book1, Genre = fantasyGenre });
        book1.BookTropes.Add(new BookTrope { Book = book1, Trope = magicTrope });

        var book2 = new Book { Title = "Book 2", Author = "Author B" };
        book2.BookGenres.Add(new BookGenre { Book = book2, Genre = sciFiGenre });
        book2.BookTropes.Add(new BookTrope { Book = book2, Trope = spaceTrope });

        await _repository.AddAsync(book1);
        await _repository.AddAsync(book2);
        await _context.SaveChangesAsync();

        // Act - Search by Genre
        var fantasyBooks = await _repository.SearchBooksAsync("Fantasy");
        var sciFiBooks = await _repository.SearchBooksAsync("Sci-Fi");

        // Act - Search by Trope
        var magicBooks = await _repository.SearchBooksAsync("Magic");
        var spaceBooks = await _repository.SearchBooksAsync("Space");

        // Assert
        fantasyBooks.Should().ContainSingle().Which.Title.Should().Be("Book 1");
        sciFiBooks.Should().ContainSingle().Which.Title.Should().Be("Book 2");
        magicBooks.Should().ContainSingle().Which.Title.Should().Be("Book 1");
        spaceBooks.Should().ContainSingle().Which.Title.Should().Be("Book 2");
    }
}
