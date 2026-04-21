using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
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
    private readonly IShareCardService _shareCardService;
    private readonly IImageService _imageService;
    private readonly BookDetailViewModel _viewModel;

    public BookDetailViewModelTests()
    {
        BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.MarkAsInitialized();

        _bookService = Substitute.For<IBookService>();
        _progressService = Substitute.For<IProgressService>();
        _quoteService = Substitute.For<IQuoteService>();
        _annotationService = Substitute.For<IAnnotationService>();
        _genreService = Substitute.For<IGenreService>();
        _shareCardService = Substitute.For<IShareCardService>();
        _imageService = Substitute.For<IImageService>();

        _viewModel = new BookDetailViewModel(
            _bookService,
            _progressService,
            _quoteService,
            _annotationService,
            _genreService,
            _shareCardService,
            _imageService);
    }

    [Fact]
    public async Task AddSessionAsync_Should_Reload_Book_When_Session_Added()
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
        await _bookService.Received(1).GetWithDetailsAsync(book.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteBookAsync_Should_Reload_Book_After_Completion()
    {
        // Arrange
        var book = new Book { Id = Guid.NewGuid(), Title = "Test", Author = "Author" };
        _viewModel.Book = book;

        StubReload(book);

        // Act
        await _viewModel.CompleteBookAsync();

        // Assert
        await _bookService.Received(1).CompleteBookAsync(book.Id, Arg.Any<CancellationToken>());
        await _bookService.Received(1).GetWithDetailsAsync(book.Id, Arg.Any<CancellationToken>());
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

    [Fact]
    public async Task LoadAsync_BookNotFound_SetsError()
    {
        var id = Guid.NewGuid();
        _bookService.GetWithDetailsAsync(id, Arg.Any<CancellationToken>()).Returns((Book?)null);

        await _viewModel.LoadCommand.ExecuteAsync(id);

        _viewModel.ErrorMessage.Should().NotBeNull();
        _viewModel.ErrorMessage!.Should().Contain("Book not found");
    }

    [Fact]
    public async Task LoadAsync_Existing_PopulatesCollections()
    {
        var book = new Book { Id = Guid.NewGuid(), Title = "Test" };
        StubReload(book);
        _quoteService.GetQuotesByBookAsync(book.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { new Quote { Text = "Q" } });
        _annotationService.GetAnnotationsByBookAsync(book.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { new Annotation { Note = "A" } });
        _genreService.GetGenresForBookAsync(book.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { new Genre { Name = "SF" } });

        await _viewModel.LoadCommand.ExecuteAsync(book.Id);

        _viewModel.Book.Should().Be(book);
        _viewModel.Quotes.Should().HaveCount(1);
        _viewModel.Annotations.Should().HaveCount(1);
        _viewModel.BookGenres.Should().HaveCount(1);
    }

    [Fact]
    public async Task StartReadingAsync_BookNull_IsNoOp()
    {
        _viewModel.Book = null;

        await _viewModel.StartReadingCommand.ExecuteAsync(null);

        await _bookService.DidNotReceive().StartReadingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartReadingAsync_WithBook_CallsService()
    {
        var book = new Book { Id = Guid.NewGuid() };
        _viewModel.Book = book;
        StubReload(book);

        await _viewModel.StartReadingCommand.ExecuteAsync(null);

        await _bookService.Received(1).StartReadingAsync(book.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteBookAsync_BookNull_IsNoOp()
    {
        _viewModel.Book = null;

        await _viewModel.CompleteBookCommand.ExecuteAsync(null);

        await _bookService.DidNotReceive().CompleteBookAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddSessionAsync_BookNull_IsNoOp()
    {
        _viewModel.Book = null;

        await _viewModel.AddSessionAsync(15);

        await _progressService.DidNotReceive().AddSessionAsync(Arg.Any<ReadingSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddSessionAsync_ZeroMinutes_IsNoOp()
    {
        _viewModel.Book = new Book { Id = Guid.NewGuid() };

        await _viewModel.AddSessionAsync(0);

        await _progressService.DidNotReceive().AddSessionAsync(Arg.Any<ReadingSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddQuoteAsync_AddsAndReloads()
    {
        var book = new Book { Id = Guid.NewGuid() };
        _viewModel.Book = book;
        StubReload(book);

        await _viewModel.AddQuoteAsync("Meaningful", 42);

        await _quoteService.Received(1).AddAsync(
            Arg.Is<Quote>(q => q.Text == "Meaningful" && q.PageNumber == 42 && q.BookId == book.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddQuoteAsync_BookNull_IsNoOp()
    {
        _viewModel.Book = null;

        await _viewModel.AddQuoteAsync("txt", 1);

        await _quoteService.DidNotReceive().AddAsync(Arg.Any<Quote>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(RatingCategory.Characters)]
    [InlineData(RatingCategory.Plot)]
    [InlineData(RatingCategory.WritingStyle)]
    [InlineData(RatingCategory.SpiceLevel)]
    [InlineData(RatingCategory.Pacing)]
    [InlineData(RatingCategory.WorldBuilding)]
    [InlineData(RatingCategory.Spannung)]
    [InlineData(RatingCategory.Humor)]
    [InlineData(RatingCategory.Informationsgehalt)]
    [InlineData(RatingCategory.EmotionaleTiefe)]
    [InlineData(RatingCategory.Atmosphaere)]
    public async Task UpdateRatingAsync_UpdatesCategory(RatingCategory category)
    {
        var book = new Book { Id = Guid.NewGuid() };
        _viewModel.Book = book;
        StubReload(book);

        await _viewModel.UpdateRatingAsync(category, 4);

        await _bookService.Received(1).UpdateAsync(book, Arg.Any<CancellationToken>());
        _viewModel.GetRating(category).Should().Be(4);
    }

    [Fact]
    public async Task UpdateRatingAsync_BookNull_IsNoOp()
    {
        _viewModel.Book = null;

        await _viewModel.UpdateRatingAsync(RatingCategory.Plot, 5);

        await _bookService.DidNotReceive().UpdateAsync(Arg.Any<Book>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GetRating_BookNull_ReturnsNull()
    {
        _viewModel.Book = null;

        _viewModel.GetRating(RatingCategory.Plot).Should().BeNull();
    }

    [Fact]
    public async Task UpdateNotesAsync_UpdatesBookAndSaves()
    {
        var book = new Book { Id = Guid.NewGuid(), Notes = "old" };
        _viewModel.Book = book;

        await _viewModel.UpdateNotesAsync("new notes");

        book.Notes.Should().Be("new notes");
        await _bookService.Received(1).UpdateAsync(book, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateNotesAsync_BookNull_IsNoOp()
    {
        _viewModel.Book = null;

        await _viewModel.UpdateNotesAsync("x");

        await _bookService.DidNotReceive().UpdateAsync(Arg.Any<Book>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAndShareBookCardAsync_BookNull_IsNoOp()
    {
        _viewModel.Book = null;

        await _viewModel.GenerateAndShareBookCardCommand.ExecuteAsync(null);

        await _shareCardService.DidNotReceive().GenerateBookCardAsync(Arg.Any<BookShareData>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAndShareBookCardAsync_InvokesEvent()
    {
        var book = new Book { Id = Guid.NewGuid(), Title = "T", Author = "A" };
        _viewModel.Book = book;
        _imageService.GetResizedCoverImageAsync(book.Id, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(byte[] Bytes, string ContentType)?>((new byte[] { 1, 2, 3 }, "image/png")));
        _shareCardService.GenerateBookCardAsync(Arg.Any<BookShareData>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));

        byte[]? captured = null;
        _viewModel.BookShareCardReady += bytes => captured = bytes;

        await _viewModel.GenerateAndShareBookCardCommand.ExecuteAsync(null);

        captured.Should().NotBeNull();
        captured!.Length.Should().BeGreaterThan(0);
        _viewModel.IsGeneratingBookCard.Should().BeFalse();
    }
}