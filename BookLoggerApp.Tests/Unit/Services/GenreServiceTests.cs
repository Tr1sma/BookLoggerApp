using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.Services;

public class GenreServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;
    private readonly GenreService _service;

    public GenreServiceTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);
        _cache = Substitute.For<IMemoryCache>();
        
        // Setup cache to return false for TryGetValue by default causing database hit
        object? outValue = null;
        _cache.TryGetValue(Arg.Any<object>(), out outValue).Returns(x => 
        {
            x[1] = null;
            return false;
        });

        _service = new GenreService(_unitOfWork, _cache);
        
        // Clear seeded genres to ensure tests start with empty state
        _context.Genres.RemoveRange(_context.Genres);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_Genres_From_Db_When_Not_Cached()
    {
        // Arrange
        _context.Genres.Add(new Genre { Name = "Fantasy" });
        _context.Genres.Add(new Genre { Name = "Sci-Fi" });
        await _context.SaveChangesAsync();

        // Act
        var genres = await _service.GetAllAsync();

        // Assert
        genres.Should().HaveCount(2);
        _cache.Received(1).CreateEntry(Arg.Any<object>()); // Should cache result
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_Genres_From_Cache_When_Cached()
    {
        // Arrange (Cache Hit)
        var cachedGenres = new List<Genre> { new Genre { Name = "Cached Genre" } };
        object? outValue;
        _cache.TryGetValue(Arg.Any<object>(), out outValue).Returns(x => 
        {
            x[1] = cachedGenres;
            return true;
        });

        // Act
        var genres = await _service.GetAllAsync();

        // Assert
        genres.Should().HaveCount(1);
        genres.First().Name.Should().Be("Cached Genre");
        // Should NOT access DB (empty DB would return 0 if accessed)
    }

    [Fact]
    public async Task AddAsync_Should_Invalidate_Cache()
    {
        // Arrange
        var genre = new Genre { Name = "New Genre" };

        // Act
        await _service.AddAsync(genre);

        // Assert
        _cache.Received(1).Remove(Arg.Any<object>());
        var dbGenre = await _context.Genres.FirstOrDefaultAsync();
        dbGenre.Should().NotBeNull();
        dbGenre!.Name.Should().Be("New Genre");
    }

    [Fact]
    public async Task UpdateAsync_Should_Invalidate_Cache()
    {
        // Arrange
        var genre = new Genre { Name = "Old Name" };
        _context.Genres.Add(genre);
        await _context.SaveChangesAsync();

        genre.Name = "New Name";

        // Act
        await _service.UpdateAsync(genre);

        // Assert
        _cache.Received(1).Remove(Arg.Any<object>());
        var dbGenre = await _context.Genres.FindAsync(genre.Id);
        dbGenre!.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task DeleteAsync_Should_Invalidate_Cache()
    {
        // Arrange
        var genre = new Genre { Name = "To Delete" };
        _context.Genres.Add(genre);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteAsync(genre.Id);

        // Assert
        _cache.Received(1).Remove(Arg.Any<object>());
        var count = await _context.Genres.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task AddGenreToBookAsync_Should_Add_Relation()
    {
        // Arrange
        var book = new Book { Title = "Book" };
        var genre = new Genre { Name = "Genre" };
        _context.Books.Add(book);
        _context.Genres.Add(genre);
        await _context.SaveChangesAsync();

        // Act
        await _service.AddGenreToBookAsync(book.Id, genre.Id);

        // Assert
        var relation = await _context.BookGenres.FirstOrDefaultAsync();
        relation.Should().NotBeNull();
        relation!.BookId.Should().Be(book.Id);
        relation.GenreId.Should().Be(genre.Id);
    }

    [Fact]
    public async Task GetGenresForBookAsync_Should_Return_Associated_Genres()
    {
        // Arrange
        var book = new Book { Title = "Book" };
        var genre1 = new Genre { Name = "G1" };
        var genre2 = new Genre { Name = "G2" };
        _context.Books.Add(book);
        _context.Genres.AddRange(genre1, genre2);
        await _context.SaveChangesAsync();

        _context.BookGenres.Add(new BookGenre { BookId = book.Id, GenreId = genre1.Id });
        await _context.SaveChangesAsync();

        // Act
        var genres = await _service.GetGenresForBookAsync(book.Id);

        // Assert
        genres.Should().HaveCount(1);
        genres.First().Name.Should().Be("G1");
    }
}
