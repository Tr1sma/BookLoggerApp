using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories.Specific;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Repositories;

public class ReadingSessionRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ReadingSessionRepository _repository;
    private readonly BookRepository _bookRepository;

    public ReadingSessionRepositoryTests()
    {
        _context = TestDbContext.Create();
        _repository = new ReadingSessionRepository(_context);
        _bookRepository = new BookRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetSessionsByBookAsync_ShouldReturnOnlySessionsForBook()
    {
        // Arrange
        var book1 = await _bookRepository.AddAsync(new Book { Title = "Book 1", Author = "Author" });
        var book2 = await _bookRepository.AddAsync(new Book { Title = "Book 2", Author = "Author" });

        await _repository.AddAsync(new ReadingSession { BookId = book1.Id, Minutes = 30 });
        await _repository.AddAsync(new ReadingSession { BookId = book1.Id, Minutes = 45 });
        await _repository.AddAsync(new ReadingSession { BookId = book2.Id, Minutes = 60 });

        // Act
        var sessions = await _repository.GetSessionsByBookAsync(book1.Id);

        // Assert
        sessions.Should().HaveCount(2);
        sessions.Should().OnlyContain(s => s.BookId == book1.Id);
    }

    [Fact]
    public async Task GetTotalMinutesReadAsync_ShouldSumMinutesCorrectly()
    {
        // Arrange
        var book = await _bookRepository.AddAsync(new Book { Title = "Test Book", Author = "Author" });
        await _repository.AddAsync(new ReadingSession { BookId = book.Id, Minutes = 30 });
        await _repository.AddAsync(new ReadingSession { BookId = book.Id, Minutes = 45 });
        await _repository.AddAsync(new ReadingSession { BookId = book.Id, Minutes = 15 });

        // Act
        var totalMinutes = await _repository.GetTotalMinutesReadAsync(book.Id);

        // Assert
        totalMinutes.Should().Be(90);
    }

    [Fact]
    public async Task GetTotalPagesReadAsync_ShouldSumPagesCorrectly()
    {
        // Arrange
        var book = await _bookRepository.AddAsync(new Book { Title = "Test Book", Author = "Author" });
        await _repository.AddAsync(new ReadingSession { BookId = book.Id, PagesRead = 20 });
        await _repository.AddAsync(new ReadingSession { BookId = book.Id, PagesRead = 30 });
        await _repository.AddAsync(new ReadingSession { BookId = book.Id, PagesRead = null }); // Should be ignored

        // Act
        var totalPages = await _repository.GetTotalPagesReadAsync(book.Id);

        // Assert
        totalPages.Should().Be(50);
    }

    [Fact]
    public async Task GetSessionsInRangeAsync_ShouldReturnSessionsWithinDateRange()
    {
        // Arrange
        var book = await _bookRepository.AddAsync(new Book { Title = "Test Book", Author = "Author" });
        var today = DateTime.UtcNow.Date;

        await _repository.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-5),
            Minutes = 30
        });
        await _repository.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(-2),
            Minutes = 45
        });
        await _repository.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = today.AddDays(2),
            Minutes = 60
        });

        // Act
        var sessions = await _repository.GetSessionsInRangeAsync(today.AddDays(-3), today);

        // Assert
        sessions.Should().HaveCount(1);
        sessions.First().Minutes.Should().Be(45);
    }

    [Fact]
    public async Task GetRecentSessionsAsync_ShouldReturnMostRecentSessions()
    {
        // Arrange
        var book = await _bookRepository.AddAsync(new Book { Title = "Test Book", Author = "Author" });

        await _repository.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = DateTime.UtcNow.AddDays(-3),
            Minutes = 30
        });
        await Task.Delay(10);
        await _repository.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = DateTime.UtcNow.AddDays(-2),
            Minutes = 45
        });
        await Task.Delay(10);
        await _repository.AddAsync(new ReadingSession
        {
            BookId = book.Id,
            StartedAt = DateTime.UtcNow.AddDays(-1),
            Minutes = 60
        });

        // Act
        var recentSessions = await _repository.GetRecentSessionsAsync(2);

        // Assert
        recentSessions.Should().HaveCount(2);
        recentSessions.First().Minutes.Should().Be(60);
    }
}
