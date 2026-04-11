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
    private readonly ITimerStateService _timerStateService;
    private readonly IShareCardService _shareCardService;
    private readonly IImageService _imageService;
    private readonly ReadingViewModel _viewModel;

    public ReadingViewModelTests()
    {
        BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.MarkAsInitialized();
        _progressService = Substitute.For<IProgressService>();
        _bookService = Substitute.For<IBookService>();
        _progressionService = Substitute.For<IProgressionService>();
        _timerStateService = Substitute.For<ITimerStateService>();
        _shareCardService = Substitute.For<IShareCardService>();
        _imageService = Substitute.For<IImageService>();

        _viewModel = new ReadingViewModel(_progressService, _bookService, _progressionService, _timerStateService, _shareCardService, _imageService);
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
        await _viewModel.UpdatePageCommand.ExecuteAsync(20);

        // Assert
        _viewModel.CurrentPage.Should().Be(20);
        await _progressService.Received().UpdateSessionAsync(session);
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
        _viewModel.HasReviewPromptMoment.Should().BeFalse();
        _viewModel.XpEarned.Should().Be(100);
    }

    [Fact]
    public async Task OnSessionCelebrationClose_Should_Clear_SessionCelebration_When_No_LevelUp()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, CurrentPage = 10 };
        var session = new ReadingSession { Id = Guid.NewGuid(), BookId = bookId, StartedAt = DateTime.UtcNow };
        var endResult = new SessionEndResult
        {
            Session = session,
            ProgressionResult = new ProgressionResult { XpEarned = 50 },
            GoalCompleted = true
        };

        _bookService.GetByIdAsync(bookId).Returns(book);
        _progressService.StartSessionAsync(bookId).Returns(session);
        _progressService.EndSessionAsync(session.Id, Arg.Any<int>()).Returns(endResult);

        await _viewModel.StartCommand.ExecuteAsync(bookId);
        await _viewModel.EndSessionCommand.ExecuteAsync(null);
        _viewModel.HasReviewPromptMoment.Should().BeTrue();

        // Act
        await _viewModel.OnSessionCelebrationClose();

        // Assert
        _viewModel.HasReviewPromptMoment.Should().BeFalse();
        _viewModel.ShowSessionCelebration.Should().BeFalse();
        _viewModel.ShowLevelUpCelebration.Should().BeFalse();
    }

    [Fact]
    public async Task OnSessionCelebrationClose_Should_Show_StreakCelebration_When_StreakBonusExists()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, CurrentPage = 10 };
        var session = new ReadingSession { Id = Guid.NewGuid(), BookId = bookId, StartedAt = DateTime.UtcNow };
        var endResult = new SessionEndResult
        {
            Session = session,
            ProgressionResult = new ProgressionResult
            {
                XpEarned = 250,
                StreakDays = 2,
                StreakBonusXp = 200
            }
        };

        _bookService.GetByIdAsync(bookId).Returns(book);
        _progressService.StartSessionAsync(bookId).Returns(session);
        _progressService.EndSessionAsync(session.Id, Arg.Any<int>()).Returns(endResult);

        await _viewModel.StartCommand.ExecuteAsync(bookId);
        await _viewModel.EndSessionCommand.ExecuteAsync(null);

        // Act
        await _viewModel.OnSessionCelebrationClose();

        // Assert
        _viewModel.ShowSessionCelebration.Should().BeFalse();
        _viewModel.ShowStreakCelebration.Should().BeTrue();
    }

    [Fact]
    public async Task OnLevelUpCelebrationClose_Should_Clear_LevelUp()
    {
        // Arrange
        _viewModel.LevelUpResult = new LevelUpResult
        {
            OldLevel = 5,
            NewLevel = 6,
            CoinsAwarded = 10,
            NewTotalCoins = 110
        };
        _viewModel.ShowLevelUpCelebration = true;
        _viewModel.HasReviewPromptMoment.Should().BeTrue();

        // Act
        await _viewModel.OnLevelUpCelebrationClose();

        // Assert
        _viewModel.HasReviewPromptMoment.Should().BeFalse();
        _viewModel.LevelUpResult.Should().BeNull();
        _viewModel.ShowLevelUpCelebration.Should().BeFalse();
    }

    [Fact]
    public async Task OnStreakCelebrationClose_Should_Show_LevelUp_When_Pending()
    {
        // Arrange
        _viewModel.ShowStreakCelebration = true;
        _viewModel.LevelUpResult = new LevelUpResult
        {
            OldLevel = 2,
            NewLevel = 3,
            CoinsAwarded = 150,
            NewTotalCoins = 250
        };

        // Act
        await _viewModel.OnStreakCelebrationClose();

        // Assert
        _viewModel.ShowStreakCelebration.Should().BeFalse();
        _viewModel.ShowLevelUpCelebration.Should().BeTrue();
    }

    [Fact]
    public async Task EndSessionAsync_Should_Detect_BookCompletion_And_Show_Celebration()
    {
        // Arrange: book with PageCount, user reads to last page
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, CurrentPage = 90, PageCount = 100, Status = ReadingStatus.Reading };
        var completedBook = new Book { Id = bookId, CurrentPage = 100, PageCount = 100, Status = ReadingStatus.Completed };
        var session = new ReadingSession { Id = Guid.NewGuid(), BookId = bookId, StartedAt = DateTime.UtcNow };

        var endResult = new SessionEndResult
        {
            Session = session,
            ProgressionResult = new ProgressionResult { XpEarned = 50 }
        };

        var completionXp = new ProgressionResult { XpEarned = 100, BookCompletionXp = 100 };

        _bookService.GetByIdAsync(bookId).Returns(book, completedBook);
        _progressService.StartSessionAsync(bookId).Returns(session);
        _progressService.EndSessionAsync(session.Id, Arg.Any<int>()).Returns(endResult);
        _bookService.UpdateProgressAsync(bookId, 100).Returns(completionXp);

        await _viewModel.StartCommand.ExecuteAsync(bookId);

        // Simulate user changing page to last page during session
        _viewModel.CurrentPage = 100;

        // Act: end the session
        await _viewModel.EndSessionCommand.ExecuteAsync(null);

        // Assert: book completion celebration shows directly (skips session XP modal)
        _viewModel.ShowSessionCelebration.Should().BeFalse();
        _viewModel.ShowBookCompletionCelebration.Should().BeTrue();
        _viewModel.HasReviewPromptMoment.Should().BeTrue();
        _viewModel.XpEarned.Should().Be(150); // 50 session + 100 completion
    }

    [Fact]
    public async Task EndSessionAsync_Should_Not_Double_Award_Completion_XP()
    {
        // Arrange: book auto-completes during UpdateProgressAsync
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, CurrentPage = 95, PageCount = 100, Status = ReadingStatus.Reading };
        var completedBook = new Book { Id = bookId, CurrentPage = 100, PageCount = 100, Status = ReadingStatus.Completed };
        var session = new ReadingSession { Id = Guid.NewGuid(), BookId = bookId, StartedAt = DateTime.UtcNow };

        var endResult = new SessionEndResult
        {
            Session = session,
            ProgressionResult = new ProgressionResult { XpEarned = 30 }
        };

        var completionXp = new ProgressionResult { XpEarned = 100, BookCompletionXp = 100 };

        _bookService.GetByIdAsync(bookId).Returns(book, completedBook);
        _progressService.StartSessionAsync(bookId).Returns(session);
        _progressService.EndSessionAsync(session.Id, Arg.Any<int>()).Returns(endResult);
        _bookService.UpdateProgressAsync(bookId, 100).Returns(completionXp);

        await _viewModel.StartCommand.ExecuteAsync(bookId);
        _viewModel.CurrentPage = 100;

        // Act
        await _viewModel.EndSessionCommand.ExecuteAsync(null);

        // Assert: AwardBookCompletionXpAsync should NOT be called (BookService already awards it)
        await _progressionService.DidNotReceive().AwardBookCompletionXpAsync(Arg.Any<Guid?>());
    }

    [Fact]
    public async Task OnBookCompletionCelebrationClose_Should_Show_StreakCelebration_When_StreakBonusExists()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, CurrentPage = 90, PageCount = 100, Status = ReadingStatus.Reading };
        var completedBook = new Book { Id = bookId, CurrentPage = 100, PageCount = 100, Status = ReadingStatus.Completed };
        var session = new ReadingSession { Id = Guid.NewGuid(), BookId = bookId, StartedAt = DateTime.UtcNow };

        var endResult = new SessionEndResult
        {
            Session = session,
            ProgressionResult = new ProgressionResult
            {
                XpEarned = 250,
                StreakDays = 2,
                StreakBonusXp = 200
            }
        };

        var completionXp = new ProgressionResult { XpEarned = 100, BookCompletionXp = 100 };

        _bookService.GetByIdAsync(bookId).Returns(book, completedBook);
        _progressService.StartSessionAsync(bookId).Returns(session);
        _progressService.EndSessionAsync(session.Id, Arg.Any<int>()).Returns(endResult);
        _bookService.UpdateProgressAsync(bookId, 100).Returns(completionXp);

        await _viewModel.StartCommand.ExecuteAsync(bookId);
        _viewModel.CurrentPage = 100;
        await _viewModel.EndSessionCommand.ExecuteAsync(null);

        // Act
        await _viewModel.OnBookCompletionCelebrationClose();

        // Assert
        _viewModel.ShowBookCompletionCelebration.Should().BeFalse();
        _viewModel.ShowStreakCelebration.Should().BeTrue();
    }

    [Fact]
    public async Task OnBookCompletionCelebrationClose_Should_Preserve_ReviewPromptMoment_For_StreakFollowUp()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, CurrentPage = 90, PageCount = 100, Status = ReadingStatus.Reading };
        var completedBook = new Book { Id = bookId, CurrentPage = 100, PageCount = 100, Status = ReadingStatus.Completed };
        var session = new ReadingSession { Id = Guid.NewGuid(), BookId = bookId, StartedAt = DateTime.UtcNow };

        var endResult = new SessionEndResult
        {
            Session = session,
            ProgressionResult = new ProgressionResult
            {
                XpEarned = 250,
                StreakDays = 2,
                StreakBonusXp = 200
            }
        };

        var completionXp = new ProgressionResult { XpEarned = 100, BookCompletionXp = 100 };

        _bookService.GetByIdAsync(bookId).Returns(book, completedBook);
        _progressService.StartSessionAsync(bookId).Returns(session);
        _progressService.EndSessionAsync(session.Id, Arg.Any<int>()).Returns(endResult);
        _bookService.UpdateProgressAsync(bookId, 100).Returns(completionXp);

        await _viewModel.StartCommand.ExecuteAsync(bookId);
        _viewModel.CurrentPage = 100;
        await _viewModel.EndSessionCommand.ExecuteAsync(null);

        // Act
        await _viewModel.OnBookCompletionCelebrationClose();

        // Assert
        _viewModel.ShowStreakCelebration.Should().BeTrue();
        _viewModel.HasReviewPromptMoment.Should().BeTrue();
    }

    [Fact]
    public async Task OnStreakCelebrationClose_Should_Show_LevelUp_After_BookCompletion_Sequence()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, CurrentPage = 90, PageCount = 100, Status = ReadingStatus.Reading };
        var completedBook = new Book { Id = bookId, CurrentPage = 100, PageCount = 100, Status = ReadingStatus.Completed };
        var session = new ReadingSession { Id = Guid.NewGuid(), BookId = bookId, StartedAt = DateTime.UtcNow };

        var endResult = new SessionEndResult
        {
            Session = session,
            ProgressionResult = new ProgressionResult
            {
                XpEarned = 250,
                StreakDays = 2,
                StreakBonusXp = 200,
                LevelUp = new LevelUpResult
                {
                    OldLevel = 2,
                    NewLevel = 3,
                    CoinsAwarded = 150,
                    NewTotalCoins = 250
                }
            }
        };

        var completionXp = new ProgressionResult { XpEarned = 100, BookCompletionXp = 100 };

        _bookService.GetByIdAsync(bookId).Returns(book, completedBook);
        _progressService.StartSessionAsync(bookId).Returns(session);
        _progressService.EndSessionAsync(session.Id, Arg.Any<int>()).Returns(endResult);
        _bookService.UpdateProgressAsync(bookId, 100).Returns(completionXp);

        await _viewModel.StartCommand.ExecuteAsync(bookId);
        _viewModel.CurrentPage = 100;
        await _viewModel.EndSessionCommand.ExecuteAsync(null);
        await _viewModel.OnBookCompletionCelebrationClose();

        // Act
        await _viewModel.OnStreakCelebrationClose();

        // Assert
        _viewModel.ShowStreakCelebration.Should().BeFalse();
        _viewModel.ShowLevelUpCelebration.Should().BeTrue();
    }

    [Fact]
    public async Task OnStreakCelebrationClose_Should_Clear_Preserved_ReviewPromptMoment_When_NoFurtherCelebration()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, CurrentPage = 90, PageCount = 100, Status = ReadingStatus.Reading };
        var completedBook = new Book { Id = bookId, CurrentPage = 100, PageCount = 100, Status = ReadingStatus.Completed };
        var session = new ReadingSession { Id = Guid.NewGuid(), BookId = bookId, StartedAt = DateTime.UtcNow };

        var endResult = new SessionEndResult
        {
            Session = session,
            ProgressionResult = new ProgressionResult
            {
                XpEarned = 250,
                StreakDays = 2,
                StreakBonusXp = 200
            }
        };

        var completionXp = new ProgressionResult { XpEarned = 100, BookCompletionXp = 100 };

        _bookService.GetByIdAsync(bookId).Returns(book, completedBook);
        _progressService.StartSessionAsync(bookId).Returns(session);
        _progressService.EndSessionAsync(session.Id, Arg.Any<int>()).Returns(endResult);
        _bookService.UpdateProgressAsync(bookId, 100).Returns(completionXp);

        await _viewModel.StartCommand.ExecuteAsync(bookId);
        _viewModel.CurrentPage = 100;
        await _viewModel.EndSessionCommand.ExecuteAsync(null);
        await _viewModel.OnBookCompletionCelebrationClose();

        // Act
        await _viewModel.OnStreakCelebrationClose();

        // Assert
        _viewModel.ShowStreakCelebration.Should().BeFalse();
        _viewModel.HasReviewPromptMoment.Should().BeFalse();
    }
}
