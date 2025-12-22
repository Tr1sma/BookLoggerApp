using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Tests.Integration;

public class ShelfServiceTests : IDisposable
{
    private readonly TestDbContextFactory _contextFactory;
    private readonly ShelfService _shelfService;
    private readonly string _dbName;

    public ShelfServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _contextFactory = new TestDbContextFactory(_dbName);
        _shelfService = new ShelfService(_contextFactory);
    }

    public void Dispose()
    {
        using var context = _contextFactory.CreateDbContext();
        context.Database.EnsureDeleted();
        context.Dispose();
    }

    [Fact]
    public async Task GetBooksForShelfAsync_ShouldIncludeUsesCoverAsSpine_WhenTrue()
    {
        // Arrange
        using (var context = _contextFactory.CreateDbContext())
        {
            var shelf = new Shelf { Name = "Test Shelf" };
            var book = new Book
            {
                Title = "Cover Mode Book",
                Author = "A",
                UsesCoverAsSpine = true,
                CoverImagePath = "img.jpg"
            };

            context.Shelves.Add(shelf);
            context.Books.Add(book);

            // Add book to shelf directly (simulating what AddBookToShelfAsync does)
            context.BookShelves.Add(new BookShelf { ShelfId = shelf.Id, BookId = book.Id, Position = 0 });

            await context.SaveChangesAsync();
        }

        // Act
        var shelves = await _shelfService.GetAllShelvesAsync();
        var shelfId = shelves.First().Id;
        var books = await _shelfService.GetBooksForShelfAsync(shelfId);

        // Assert
        books.Should().HaveCount(1);
        books.First().UsesCoverAsSpine.Should().BeTrue();
    }

    [Fact]
    public async Task GetBooksForShelfAsync_ShouldIncludeUsesCoverAsSpine_WhenFalse()
    {
        // Arrange
        using (var context = _contextFactory.CreateDbContext())
        {
            var shelf = new Shelf { Name = "Test Shelf 2" };
            var book = new Book
            {
                Title = "Color Mode Book",
                Author = "A",
                UsesCoverAsSpine = false, // Explicitly false
                SpineColor = "red"
            };

            context.Shelves.Add(shelf);
            context.Books.Add(book);
            context.BookShelves.Add(new BookShelf { ShelfId = shelf.Id, BookId = book.Id, Position = 0 });

            await context.SaveChangesAsync();
        }

        // Act
        var shelves = await _shelfService.GetAllShelvesAsync();
        var shelfId = shelves.First().Id;
        var books = await _shelfService.GetBooksForShelfAsync(shelfId);

        // Assert
        books.Should().HaveCount(1);
        books.First().UsesCoverAsSpine.Should().BeFalse();
        books.First().SpineColor.Should().Be("red");
    }
}
