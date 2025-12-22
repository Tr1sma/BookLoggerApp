using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class StatsServiceBasicTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly StatsService _service;

    public StatsServiceBasicTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);
        _service = new StatsService(_unitOfWork);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task GetTotalMinutesReadAsync_ShouldReturnCorrectTotal()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, Title = "Test Book", Author = "Author", Status = ReadingStatus.Reading };
        await _unitOfWork.Books.AddAsync(book);

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = bookId,
            Minutes = 30,
            StartedAt = DateTime.UtcNow.AddHours(-2)
        });

        await _unitOfWork.ReadingSessions.AddAsync(new ReadingSession
        {
            BookId = bookId,
            Minutes = 45,
            StartedAt = DateTime.UtcNow.AddHours(-1)
        });

        await _context.SaveChangesAsync();

        // Act
        var totalMinutes = await _service.GetTotalMinutesReadAsync();

        // Assert
        totalMinutes.Should().Be(75);
    }
}
