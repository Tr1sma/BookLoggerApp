using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class BookEditViewModelTests
{
    private readonly IBookService _bookService;
    private readonly IGenreService _genreService;
    private readonly ILookupService _lookupService;
    private readonly IImageService _imageService;
    private readonly IShelfService _shelfService;
    private readonly IWishlistService _wishlistService;
    private readonly IShareCardService _shareCardService;
    private readonly IProgressService _progressService;
    private readonly BookEditViewModel _viewModel;

    public BookEditViewModelTests()
    {
        BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.MarkAsInitialized();
        _bookService = Substitute.For<IBookService>();
        _genreService = Substitute.For<IGenreService>();
        _lookupService = Substitute.For<ILookupService>();
        _imageService = Substitute.For<IImageService>();
        _shelfService = Substitute.For<IShelfService>();
        _wishlistService = Substitute.For<IWishlistService>();
        _shareCardService = Substitute.For<IShareCardService>();
        _progressService = Substitute.For<IProgressService>();

        _genreService.GetAllAsync().Returns(new List<Genre>());
        _shelfService.GetAllShelvesAsync().Returns(new List<Shelf>());

        _viewModel = new BookEditViewModel(
            _bookService,
            _genreService,
            _lookupService,
            _imageService,
            _shelfService,
            _wishlistService,
            _shareCardService,
            _progressService
        );
    }

    [Fact]
    public async Task LoadAsync_Should_Initialize_New_Book_When_Id_Is_Null()
    {
        // Arrange
        _genreService.GetAllAsync().Returns(new List<Genre>());
        _shelfService.GetAllShelvesAsync().Returns(new List<Shelf>());

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Assert
        _viewModel.Book.Should().NotBeNull();
        _viewModel.Book!.Id.Should().Be(Guid.Empty);
        _viewModel.Book.Status.Should().Be(ReadingStatus.Planned);
    }

    [Fact]
    public async Task LoadAsync_Should_Load_Existing_Book_When_Id_Is_Provided()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, Title = "Existing Book" };

        _genreService.GetAllAsync().Returns(new List<Genre>());
        _shelfService.GetAllShelvesAsync().Returns(new List<Shelf>());
        _bookService.GetWithDetailsAsync(bookId, Arg.Any<CancellationToken>()).Returns(book);

        // Act
        await _viewModel.LoadCommand.ExecuteAsync(bookId);

        // Assert
        _viewModel.Book.Should().BeEquivalentTo(book);
    }

    [Fact]
    public async Task SaveAsync_Should_Show_Error_When_Title_Is_Missing()
    {
        // Arrange
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "";
        _viewModel.Book.Author = "Author";

        // Act
        await _viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        _viewModel.ErrorMessage.Should().NotBeNullOrEmpty();
        await _bookService.DidNotReceive().SaveBookWithRelationsAsync(
            Arg.Any<Book>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_Should_Save_New_Book_Through_The_Atomic_Method()
    {
        // Arrange
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "New Book";
        _viewModel.Book.Author = "Author";

        var savedBook = new Book { Id = Guid.NewGuid(), Title = "New Book" };
        _bookService.SaveBookWithRelationsAsync(
            Arg.Any<Book>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new BookSaveResult(savedBook, false, false));

        // Act
        await _viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        await _bookService.Received(1).SaveBookWithRelationsAsync(
            Arg.Is<Book>(b => b.Title == "New Book"), Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
        _viewModel.Book.Should().Be(savedBook);
    }

    [Fact]
    public async Task SaveAsync_Should_Save_Existing_Book_Through_The_Atomic_Method()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, Title = "Existing Book", Author = "Author" };

        _genreService.GetAllAsync().Returns(new List<Genre>());
        _shelfService.GetAllShelvesAsync().Returns(new List<Shelf>());
        _bookService.GetWithDetailsAsync(bookId, Arg.Any<CancellationToken>()).Returns(book);
        _bookService.SaveBookWithRelationsAsync(
            Arg.Any<Book>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ci => new BookSaveResult(ci.Arg<Book>(), false, false));

        await _viewModel.LoadCommand.ExecuteAsync(bookId);
        _viewModel.Book!.Title = "Updated Title";

        // Act
        await _viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        await _bookService.Received(1).SaveBookWithRelationsAsync(
            Arg.Is<Book>(b => b.Id == bookId && b.Title == "Updated Title"),
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_Maps_CompletionResult_To_Celebration_Flags()
    {
        // Arrange
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "Done";
        _viewModel.Book.Author = "Author";

        _bookService.SaveBookWithRelationsAsync(
            Arg.Any<Book>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ci => new BookSaveResult(ci.Arg<Book>(), ShowCompletionCelebration: true, CompletedFromExisting: true));

        // Act
        await _viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        _viewModel.ShowBookCompletionCelebration.Should().BeTrue();
        _viewModel.BookCompletedFromSession.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_NoCompletion_LeavesCelebrationFlagsUnset()
    {
        // Arrange
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "Plain";
        _viewModel.Book.Author = "Author";

        _bookService.SaveBookWithRelationsAsync(
            Arg.Any<Book>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ci => new BookSaveResult(ci.Arg<Book>(), false, false));

        // Act
        await _viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        _viewModel.ShowBookCompletionCelebration.Should().BeFalse();
        _viewModel.BookCompletedFromSession.Should().BeFalse();
    }

    [Fact]
    public async Task LookupByIsbnAsync_Should_Populate_Book_Data()
    {
        // Arrange
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.ISBN = "1234567890";

        var metadata = new BookMetadata
        {
            Title = "Lookup Title",
            Author = "Lookup Author",
            Description = "Lookup Desc"
        };
        _lookupService.LookupByISBNAsync("1234567890").Returns(metadata);

        // Act
        await _viewModel.LookupByIsbnCommand.ExecuteAsync(null);

        // Assert
        _viewModel.Book.Title.Should().Be("Lookup Title");
        _viewModel.Book.Author.Should().Be("Lookup Author");
        _viewModel.Book.Description.Should().Be("Lookup Desc");
        _viewModel.LookupMessage.Should().Contain("successfully");
        _viewModel.LookupMessageIsError.Should().BeFalse();
    }

    [Fact]
    public async Task SearchByTitleAsync_Should_Populate_Results()
    {
        // Arrange
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "The Hobbit";

        var results = new List<BookMetadata>
        {
            new() { Title = "The Hobbit", Author = "J.R.R. Tolkien", PublicationYear = 1937 },
            new() { Title = "The Hobbit (Illustrated)", Author = "J.R.R. Tolkien", PublicationYear = 2020 }
        };
        _lookupService.SearchBooksAsync(Arg.Any<string>()).Returns(results);

        // Act
        await _viewModel.SearchByTitleCommand.ExecuteAsync(null);

        // Assert
        _viewModel.TitleSearchResults.Should().HaveCount(2);
        _viewModel.LookupMessageIsError.Should().BeFalse();
    }

    [Fact]
    public async Task SearchByTitleAsync_Should_Include_Author_In_Query()
    {
        // Arrange
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "The Hobbit";
        _viewModel.Book.Author = "Tolkien";
        _lookupService.SearchBooksAsync(Arg.Any<string>()).Returns(new List<BookMetadata>());

        // Act
        await _viewModel.SearchByTitleCommand.ExecuteAsync(null);

        // Assert
        await _lookupService.Received(1).SearchBooksAsync(
            Arg.Is<string>(q => q.Contains("The Hobbit") && q.Contains("Tolkien")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchByTitleAsync_Should_Warn_When_Title_Empty()
    {
        // Arrange
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "";

        // Act
        await _viewModel.SearchByTitleCommand.ExecuteAsync(null);

        // Assert
        _viewModel.LookupMessageIsError.Should().BeTrue();
        _viewModel.TitleSearchResults.Should().BeEmpty();
        await _lookupService.DidNotReceive().SearchBooksAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchByTitleAsync_Should_Report_No_Results()
    {
        // Arrange
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "asldkfjaslkdfj";
        _lookupService.SearchBooksAsync(Arg.Any<string>()).Returns(new List<BookMetadata>());

        // Act
        await _viewModel.SearchByTitleCommand.ExecuteAsync(null);

        // Assert
        _viewModel.TitleSearchResults.Should().BeEmpty();
        _viewModel.LookupMessage.Should().Contain("No book");
        _viewModel.LookupMessageIsError.Should().BeTrue();
    }

    [Fact]
    public async Task SelectSearchResultAsync_Should_Populate_Book_And_Clear_Results()
    {
        // Arrange
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "The Hobbit";

        var results = new List<BookMetadata>
        {
            new() { Title = "The Hobbit", Author = "J.R.R. Tolkien", PublicationYear = 1937 }
        };
        _lookupService.SearchBooksAsync(Arg.Any<string>()).Returns(results);
        await _viewModel.SearchByTitleCommand.ExecuteAsync(null);

        var chosen = new BookMetadata
        {
            Title = "Chosen Title",
            Author = "Chosen Author",
            Description = "Chosen Desc"
        };

        // Act
        await _viewModel.SelectSearchResultCommand.ExecuteAsync(chosen);

        // Assert
        _viewModel.Book.Title.Should().Be("Chosen Title");
        _viewModel.Book.Author.Should().Be("Chosen Author");
        _viewModel.Book.Description.Should().Be("Chosen Desc");
        _viewModel.TitleSearchResults.Should().BeEmpty();
        _viewModel.LookupMessage.Should().Contain("successfully");
        _viewModel.LookupMessageIsError.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBookAsync_Should_Call_Delete_Service()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, Title = "Delete Me" };

        _genreService.GetAllAsync().Returns(new List<Genre>());
        _shelfService.GetAllShelvesAsync().Returns(new List<Shelf>());
        _bookService.GetWithDetailsAsync(bookId, Arg.Any<CancellationToken>()).Returns(book);

        await _viewModel.LoadCommand.ExecuteAsync(bookId);

        // Act
        await _viewModel.DeleteBookCommand.ExecuteAsync(null);

        // Assert
        await _bookService.Received(1).DeleteAsync(bookId);
        _viewModel.BookDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task OnBookCompletionCelebrationClose_Should_Hide_Celebration()
    {
        // Arrange
        _viewModel.ShowBookCompletionCelebration = true;

        // Act
        await _viewModel.OnBookCompletionCelebrationClose();

        // Assert
        _viewModel.ShowBookCompletionCelebration.Should().BeFalse();
    }
}
