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
        _genreService.GetAllAsync().Returns(new List<Genre>());
        _shelfService.GetAllShelvesAsync().Returns(new List<Shelf>());

        await _viewModel.LoadCommand.ExecuteAsync(null);

        _viewModel.Book.Should().NotBeNull();
        _viewModel.Book!.Id.Should().Be(Guid.Empty);
        _viewModel.Book.Status.Should().Be(ReadingStatus.Planned);
    }

    [Fact]
    public async Task LoadAsync_Should_Load_Existing_Book_When_Id_Is_Provided()
    {
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, Title = "Existing Book" };

        _genreService.GetAllAsync().Returns(new List<Genre>());
        _shelfService.GetAllShelvesAsync().Returns(new List<Shelf>());
        _bookService.GetWithDetailsAsync(bookId).Returns(book);

        await _viewModel.LoadCommand.ExecuteAsync(bookId);

        _viewModel.Book.Should().BeEquivalentTo(book);
    }

    [Fact]
    public async Task SaveAsync_Should_Show_Error_When_Title_Is_Missing()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "";
        _viewModel.Book.Author = "Author";

        await _viewModel.SaveCommand.ExecuteAsync(null);

        _viewModel.ErrorMessage.Should().NotBeNullOrEmpty();
        await _bookService.DidNotReceive().AddAsync(Arg.Any<Book>());
    }

    [Fact]
    public async Task SaveAsync_Should_Add_New_Book()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "New Book";
        _viewModel.Book.Author = "Author";

        var savedBook = new Book { Id = Guid.NewGuid(), Title = "New Book" };
        _bookService.AddAsync(Arg.Any<Book>()).Returns(savedBook);

        await _viewModel.SaveCommand.ExecuteAsync(null);

        await _bookService.Received(1).AddAsync(Arg.Is<Book>(b => b.Title == "New Book"));
        _viewModel.Book.Should().Be(savedBook);
    }

    [Fact]
    public async Task SaveAsync_Should_Update_Existing_Book()
    {
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, Title = "Existing Book", Author = "Author" };

        _genreService.GetAllAsync().Returns(new List<Genre>());
        _shelfService.GetAllShelvesAsync().Returns(new List<Shelf>());
        _bookService.GetWithDetailsAsync(bookId).Returns(book);
        _bookService.GetByIdAsync(bookId).Returns(book);

        await _viewModel.LoadCommand.ExecuteAsync(bookId);
        _viewModel.Book!.Title = "Updated Title";

        await _viewModel.SaveCommand.ExecuteAsync(null);

        await _bookService.Received(1).UpdateAsync(Arg.Is<Book>(b => b.Title == "Updated Title"));
    }

    [Fact]
    public async Task SaveAsync_Should_Complete_Book_Only_Once_When_Saved_Twice_Without_Status_Change()
    {
        var bookId = Guid.NewGuid();
        var dbBookPlanned = new Book { Id = bookId, Title = "Existing Book", Author = "Author", Status = ReadingStatus.Planned };
        var dbBookCompleted = new Book { Id = bookId, Title = "Existing Book", Author = "Author", Status = ReadingStatus.Completed };

        _bookService.GetWithDetailsAsync(bookId).Returns(new Book
        {
            Id = bookId,
            Title = "Existing Book",
            Author = "Author",
            Status = ReadingStatus.Planned
        });
        _bookService.GetByIdAsync(bookId).Returns(dbBookPlanned, dbBookCompleted);

        await _viewModel.LoadCommand.ExecuteAsync(bookId);
        _viewModel.Book!.Status = ReadingStatus.Completed;

        await _viewModel.SaveCommand.ExecuteAsync(null);
        await _viewModel.SaveCommand.ExecuteAsync(null);

        await _bookService.Received(2).UpdateAsync(Arg.Any<Book>());
        await _bookService.Received(1).CompleteBookAsync(bookId);
    }

    [Fact]
    public async Task SaveAsync_Should_Not_Complete_Book_When_Db_Status_Already_Completed()
    {
        var bookId = Guid.NewGuid();
        var loadedBook = new Book { Id = bookId, Title = "Existing Book", Author = "Author", Status = ReadingStatus.Planned };
        var dbBookCompleted = new Book { Id = bookId, Title = "Existing Book", Author = "Author", Status = ReadingStatus.Completed };

        _bookService.GetWithDetailsAsync(bookId).Returns(loadedBook);
        _bookService.GetByIdAsync(bookId).Returns(dbBookCompleted);

        await _viewModel.LoadCommand.ExecuteAsync(bookId);
        _viewModel.Book!.Status = ReadingStatus.Completed;

        await _viewModel.SaveCommand.ExecuteAsync(null);

        await _bookService.Received(1).UpdateAsync(Arg.Any<Book>());
        await _bookService.DidNotReceive().CompleteBookAsync(bookId);
        _viewModel.Book.Status.Should().Be(ReadingStatus.Completed);
    }

    [Fact]
    public async Task LookupByIsbnAsync_Should_Populate_Book_Data()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.ISBN = "1234567890";

        var metadata = new BookMetadata
        {
            Title = "Lookup Title",
            Author = "Lookup Author",
            Description = "Lookup Desc"
        };
        _lookupService.LookupByISBNAsync("1234567890").Returns(metadata);

        await _viewModel.LookupByIsbnCommand.ExecuteAsync(null);

        _viewModel.Book.Title.Should().Be("Lookup Title");
        _viewModel.Book.Author.Should().Be("Lookup Author");
        _viewModel.Book.Description.Should().Be("Lookup Desc");
        _viewModel.LookupMessage.Should().Contain("successfully");
        _viewModel.LookupMessageIsError.Should().BeFalse();
    }

    [Fact]
    public async Task SearchByTitleAsync_Should_Populate_Results()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "The Hobbit";

        var results = new List<BookMetadata>
        {
            new() { Title = "The Hobbit", Author = "J.R.R. Tolkien", PublicationYear = 1937 },
            new() { Title = "The Hobbit (Illustrated)", Author = "J.R.R. Tolkien", PublicationYear = 2020 }
        };
        _lookupService.SearchBooksAsync(Arg.Any<string>()).Returns(results);

        await _viewModel.SearchByTitleCommand.ExecuteAsync(null);

        _viewModel.TitleSearchResults.Should().HaveCount(2);
        _viewModel.LookupMessageIsError.Should().BeFalse();
    }

    [Fact]
    public async Task SearchByTitleAsync_Should_Include_Author_In_Query()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "The Hobbit";
        _viewModel.Book.Author = "Tolkien";
        _lookupService.SearchBooksAsync(Arg.Any<string>()).Returns(new List<BookMetadata>());

        await _viewModel.SearchByTitleCommand.ExecuteAsync(null);

        await _lookupService.Received(1).SearchBooksAsync(
            Arg.Is<string>(q => q.Contains("The Hobbit") && q.Contains("Tolkien")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchByTitleAsync_Should_Warn_When_Title_Empty()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "";

        await _viewModel.SearchByTitleCommand.ExecuteAsync(null);

        _viewModel.LookupMessageIsError.Should().BeTrue();
        _viewModel.TitleSearchResults.Should().BeEmpty();
        await _lookupService.DidNotReceive().SearchBooksAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchByTitleAsync_Should_Report_No_Results()
    {
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "asldkfjaslkdfj";
        _lookupService.SearchBooksAsync(Arg.Any<string>()).Returns(new List<BookMetadata>());

        await _viewModel.SearchByTitleCommand.ExecuteAsync(null);

        _viewModel.TitleSearchResults.Should().BeEmpty();
        _viewModel.LookupMessage.Should().Contain("No book");
        _viewModel.LookupMessageIsError.Should().BeTrue();
    }

    [Fact]
    public async Task SelectSearchResultAsync_Should_Populate_Book_And_Clear_Results()
    {
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

        await _viewModel.SelectSearchResultCommand.ExecuteAsync(chosen);

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
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, Title = "Delete Me" };

        _genreService.GetAllAsync().Returns(new List<Genre>());
        _shelfService.GetAllShelvesAsync().Returns(new List<Shelf>());
        _bookService.GetWithDetailsAsync(bookId).Returns(book);

        await _viewModel.LoadCommand.ExecuteAsync(bookId);

        await _viewModel.DeleteBookCommand.ExecuteAsync(null);

        await _bookService.Received(1).DeleteAsync(bookId);
        _viewModel.BookDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task OnBookCompletionCelebrationClose_Should_Hide_Celebration()
    {
        _viewModel.ShowBookCompletionCelebration = true;

        await _viewModel.OnBookCompletionCelebrationClose();

        _viewModel.ShowBookCompletionCelebration.Should().BeFalse();
    }
}
