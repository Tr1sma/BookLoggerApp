using FluentAssertions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Tests.TestHelpers;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Tests.Integration;

public class SpineModePersistenceTests : IDisposable
{
    private AppDbContext _context;
    private IUnitOfWork _unitOfWork;
    private MockProgressionService _progressionService;
    private MockPlantService _plantService;
    private MockGoalService _goalService;
    private BookService _bookService;
    private readonly string _dbName;

    public SpineModePersistenceTests()
    {
        _dbName = Guid.NewGuid().ToString();
        SetupServices();
    }

    private void SetupServices()
    {
        _context = TestDbContext.Create(_dbName);
        _unitOfWork = new UnitOfWork(_context);
        _progressionService = new MockProgressionService();
        _plantService = new MockPlantService();
        _goalService = new MockGoalService();
        _bookService = new BookService(_unitOfWork, _progressionService, _plantService, _goalService, null!);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task UpdateSpineMode_ShouldPersistCorrectly()
    {
        // Arrange
        var book = new Book
        {
            Title = "Spine Mode Test Book",
            Author = "Author",
            UsesCoverAsSpine = true, // Start with True (Cover Mode)
            SpineColor = "red",
            CoverImagePath = "some/path.jpg"
        };

        // Act - Save initial state
        book = await _bookService.AddAsync(book);

        // Verify initial state
        var retrievedBook = await _bookService.GetByIdAsync(book.Id);
        retrievedBook.Should().NotBeNull();
        retrievedBook!.UsesCoverAsSpine.Should().BeTrue();

        // Change to False (Color Mode)
        book.UsesCoverAsSpine = false;
        await _bookService.UpdateAsync(book);

        // Assert - Check using SAME context (verify local tracking works)
        var updatedBookSameContext = await _bookService.GetByIdAsync(book.Id);
        updatedBookSameContext.UsesCoverAsSpine.Should().BeFalse("Local context should reflect change");

        // Assert - Check using NEW context (verify DB persistence works)
        await using var newContext = TestDbContext.Create(_dbName);
        var newUnitOfWork = new UnitOfWork(newContext);
        var freshBook = await newUnitOfWork.Books.GetByIdAsync(book.Id);

        freshBook.Should().NotBeNull();
        freshBook!.UsesCoverAsSpine.Should().BeFalse("Database should have persisted the change to False");
    }

    [Fact]
    public async Task UpdateSpineMode_AndCompleteBook_ShouldPersistBoth()
    {
        // Arrange
        var book = new Book
        {
            Title = "Completion Test Book",
            Author = "Author",
            UsesCoverAsSpine = true,
            Status = ReadingStatus.Reading,
            CoverImagePath = "path/to/cover.jpg"
        };
        book = await _bookService.AddAsync(book);

        // Act - Simulate ViewModel Logic: Change Spine Mode AND Complete Book
        // In the real app, ViewModel sets properties on the SAME object instance
        book.UsesCoverAsSpine = false;

        // Emulate the logic flow we fixed in ViewModel
        // 1. UpdateAsync (saves properties)
        await _bookService.UpdateAsync(book);

        // 2. CompleteBookAsync (updates status)
        await _bookService.CompleteBookAsync(book.Id);

        // Assert - Verify Persistence
        await using var newContext = TestDbContext.Create(_dbName);
        var newUnitOfWork = new UnitOfWork(newContext);
        var retrievedBook = await newUnitOfWork.Books.GetByIdAsync(book.Id);

        retrievedBook.Should().NotBeNull();
        retrievedBook!.Status.Should().Be(ReadingStatus.Completed);
        retrievedBook.UsesCoverAsSpine.Should().BeFalse("Spine mode change should persist even when completing book");
    }
}
