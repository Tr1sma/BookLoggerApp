using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class BookDetailViewModelTests
{
    private readonly IBookService _bookService;
    private readonly IProgressService _progressService;
    private readonly IQuoteService _quoteService;
    private readonly IAnnotationService _annotationService;
    private readonly IGenreService _genreService;
    private readonly IReviewService _reviewService;
    private readonly BookDetailViewModel _viewModel;

    public BookDetailViewModelTests()
    {
        BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.MarkAsInitialized();

        _bookService = Substitute.For<IBookService>();
        _progressService = Substitute.For<IProgressService>();
        _quoteService = Substitute.For<IQuoteService>();
        _annotationService = Substitute.For<IAnnotationService>();
        _genreService = Substitute.For<IGenreService>();
        _reviewService = Substitute.For<IReviewService>();

        _viewModel = new BookDetailViewModel(
            _bookService,
            _progressService,
            _quoteService,
            _annotationService,
            _genreService,
            _reviewService);
    }

    [Fact]
    public async Task AddSessionAsync_Should_Request_Review_When_Goal_Completed()
    {
        // Arrange
        var book = new Book { Id = Guid.NewGuid(), Title = "Test", Author = "Author" };
        _viewModel.Book = book;

        _progressService.AddSessionAsync(Arg.Any<ReadingSession>()).Returns(new SessionSaveResult
        {
            Session = new ReadingSession { Id = Guid.NewGuid(), BookId = book.Id, Minutes = 15 },
            ProgressionResult = new ProgressionResult { XpEarned = 75 },
            GoalCompleted = true
        });

        StubReload(book);

        // Act
        await _viewModel.AddSessionAsync(15);

        // Assert
        await _reviewService.Received(1).TryRequestReviewAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteBookAsync_Should_Request_Review_After_Completion()
    {
        // Arrange
        var book = new Book { Id = Guid.NewGuid(), Title = "Test", Author = "Author" };
        _viewModel.Book = book;

        StubReload(book);

        // Act
        await _viewModel.CompleteBookAsync();

        // Assert
        await _bookService.Received(1).CompleteBookAsync(book.Id, Arg.Any<CancellationToken>());
        await _reviewService.Received(1).TryRequestReviewAsync(Arg.Any<CancellationToken>());
    }

    private void StubReload(Book book)
    {
        _bookService.GetWithDetailsAsync(book.Id, Arg.Any<CancellationToken>()).Returns(book);
        _progressService.GetTotalMinutesAsync(book.Id, Arg.Any<CancellationToken>()).Returns(15);
        _progressService.GetTotalPagesAsync(book.Id, Arg.Any<CancellationToken>()).Returns(0);
        _progressService.GetSessionsByBookAsync(book.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ReadingSession>());
        _quoteService.GetQuotesByBookAsync(book.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Quote>());
        _annotationService.GetAnnotationsByBookAsync(book.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Annotation>());
        _genreService.GetGenresForBookAsync(book.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Genre>());
    }
}
