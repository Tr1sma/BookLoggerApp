using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class ReadingViewModelTests
{
    private readonly IProgressService _progressService;
    private readonly IBookService _bookService;
    private readonly IProgressionService _progressionService;
    private readonly ReadingViewModel _viewModel;

    public ReadingViewModelTests()
    {
        _progressService = Substitute.For<IProgressService>();
        _bookService = Substitute.For<IBookService>();
        _progressionService = Substitute.For<IProgressionService>();

        _viewModel = new ReadingViewModel(_progressService, _bookService, _progressionService);
    }

    [Fact]
    public async Task StartAsync_Should_Create_New_Session()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, CurrentPage = 10, PageCount = 100 };
        var session = new ReadingSession { Id = Guid.NewGuid(), BookId = bookId, StartedAt = DateTime.UtcNow };

        _bookService.GetByIdAsync(bookId).Returns(book);
        _progressService.StartSessionAsync(bookId).Returns(session);

        // Act
        await _viewModel.StartCommand.ExecuteAsync(bookId);

        // Assert
        _viewModel.Session.Should().Be(session);
        _viewModel.Book.Should().Be(book);
        _viewModel.StartPage.Should().Be(10);
        _viewModel.CurrentPage.Should().Be(10);
        _viewModel.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void Pause_Should_Set_IsPaused_True()
    {
        // Act
        _viewModel.PauseCommand.Execute(null);

        // Assert
        _viewModel.IsPaused.Should().BeTrue();
    }

    [Fact]
    public void Resume_Should_Set_IsPaused_False()
    {
        // Act
        _viewModel.ResumeCommand.Execute(null);

        // Assert
        _viewModel.IsPaused.Should().BeFalse();
    }

    [Fact]
    public async Task UpdatePageAsync_Should_Update_Session_PagesRead_And_Xp()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, CurrentPage = 10 };
        var session = new ReadingSession { Id = Guid.NewGuid(), BookId = bookId, StartedAt = DateTime.UtcNow };

        _bookService.GetByIdAsync(bookId).Returns(book);
        _progressService.StartSessionAsync(bookId).Returns(session);
        await _viewModel.StartCommand.ExecuteAsync(bookId);

        // Act
        await _viewModel.UpdatePageCommand.ExecuteAsync(20); // Read 10 pages (20 - 10)

        // Assert
        _viewModel.CurrentPage.Should().Be(20);
        
        // Check session update
        // Use Arg.Any or simply verify call happened, as object reference is mutable and NSubstitute captures reference
        await _progressService.Received().UpdateSessionAsync(session);
        // Also verify property values on the session object invoked
        session.PagesRead.Should().Be(10);
        session.XpEarned.Should().Be(200);
    }

    [Fact]
    public async Task EndSessionAsync_Should_Complete_Session_And_Show_Celebration()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, CurrentPage = 10 };
        var session = new ReadingSession { Id = Guid.NewGuid(), BookId = bookId, StartedAt = DateTime.UtcNow };
        
        var endResult = new SessionEndResult 
        { 
            Session = session,
            ProgressionResult = new ProgressionResult { XpEarned = 100 }
        };

        _bookService.GetByIdAsync(bookId).Returns(book);
        _progressService.StartSessionAsync(bookId).Returns(session);
        _progressService.EndSessionAsync(session.Id, Arg.Any<int>()).Returns(endResult);

        await _viewModel.StartCommand.ExecuteAsync(bookId);

        // Act
        await _viewModel.EndSessionCommand.ExecuteAsync(null);

        // Assert
        await _progressService.Received(1).EndSessionAsync(session.Id, Arg.Any<int>());
        _viewModel.ShowSessionCelebration.Should().BeTrue();
        _viewModel.XpEarned.Should().Be(100);
    }
}
