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
    private readonly BookEditViewModel _viewModel;

    public BookEditViewModelTests()
    {
        BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.MarkAsInitialized();
        _bookService = Substitute.For<IBookService>();
        _genreService = Substitute.For<IGenreService>();
        _lookupService = Substitute.For<ILookupService>();
        _imageService = Substitute.For<IImageService>();
        _shelfService = Substitute.For<IShelfService>();

        _genreService.GetAllAsync().Returns(new List<Genre>());
        _shelfService.GetAllShelvesAsync().Returns(new List<Shelf>());

        _viewModel = new BookEditViewModel(
            _bookService,
            _genreService,
            _lookupService,
            _imageService,
            _shelfService
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
        _bookService.GetWithDetailsAsync(bookId).Returns(book);

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
        await _bookService.DidNotReceive().AddAsync(Arg.Any<Book>());
    }

    [Fact]
    public async Task SaveAsync_Should_Add_New_Book()
    {
        // Arrange
        await _viewModel.LoadCommand.ExecuteAsync(null);
        _viewModel.Book!.Title = "New Book";
        _viewModel.Book.Author = "Author";
        
        var savedBook = new Book { Id = Guid.NewGuid(), Title = "New Book" };
        _bookService.AddAsync(Arg.Any<Book>()).Returns(savedBook);

        // Act
        await _viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        await _bookService.Received(1).AddAsync(Arg.Is<Book>(b => b.Title == "New Book"));
        _viewModel.Book.Should().Be(savedBook);
    }

    [Fact]
    public async Task SaveAsync_Should_Update_Existing_Book()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, Title = "Existing Book", Author = "Author" };
        
        _genreService.GetAllAsync().Returns(new List<Genre>());
        _shelfService.GetAllShelvesAsync().Returns(new List<Shelf>());
        _bookService.GetWithDetailsAsync(bookId).Returns(book);
        _bookService.GetByIdAsync(bookId).Returns(book); // Check for existing

        await _viewModel.LoadCommand.ExecuteAsync(bookId);
        _viewModel.Book!.Title = "Updated Title";

        // Act
        await _viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        await _bookService.Received(1).UpdateAsync(Arg.Is<Book>(b => b.Title == "Updated Title"));
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
    }

    [Fact]
    public async Task DeleteBookAsync_Should_Call_Delete_Service()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, Title = "Delete Me" };
        
        _genreService.GetAllAsync().Returns(new List<Genre>());
        _shelfService.GetAllShelvesAsync().Returns(new List<Shelf>());
        _bookService.GetWithDetailsAsync(bookId).Returns(book);

        await _viewModel.LoadCommand.ExecuteAsync(bookId);

        // Act
        await _viewModel.DeleteBookCommand.ExecuteAsync(null);

        // Assert
        await _bookService.Received(1).DeleteAsync(bookId);
        _viewModel.BookDeleted.Should().BeTrue();
    }
}
