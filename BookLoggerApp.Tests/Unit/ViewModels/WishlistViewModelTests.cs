using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class WishlistViewModelTests
{
    private readonly IWishlistService _wishlistService;
    private readonly ILookupService _lookupService;
    private readonly WishlistViewModel _viewModel;

    public WishlistViewModelTests()
    {
        BookLoggerApp.Core.Infrastructure.DatabaseInitializationHelper.MarkAsInitialized();
        _wishlistService = Substitute.For<IWishlistService>();
        _lookupService = Substitute.For<ILookupService>();

        _wishlistService.GetWishlistBooksAsync().Returns(new List<Book>());

        _viewModel = new WishlistViewModel(_wishlistService, _lookupService);
    }

    [Fact]
    public async Task LookupByIsbnAsync_Should_Set_Lookup_Properties_Via_Generated_Properties()
    {
        // Arrange
        var metadata = new BookMetadata
        {
            Title = "Clean Code",
            Author = "Robert C. Martin",
            PageCount = 431,
            CoverImageUrl = "https://example.com/cover.jpg",
            Description = "A handbook of agile software craftsmanship."
        };

        _viewModel.NewIsbn = "9780132350884";
        _lookupService
            .LookupByISBNAsync("9780132350884", Arg.Any<CancellationToken>())
            .Returns(metadata);

        var changedProperties = new List<string>();
        _viewModel.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act
        await _viewModel.LookupByIsbnAsync();

        // Assert – generated properties are set (not backing fields)
        _viewModel.LookupPageCount.Should().Be(431);
        _viewModel.LookupCoverUrl.Should().Be("https://example.com/cover.jpg");
        _viewModel.LookupDescription.Should().Be("A handbook of agile software craftsmanship.");

        // Assert – INotifyPropertyChanged fired for each lookup property
        changedProperties.Should().Contain("LookupPageCount");
        changedProperties.Should().Contain("LookupCoverUrl");
        changedProperties.Should().Contain("LookupDescription");
    }

    [Fact]
    public async Task LookupByIsbnAsync_Should_Populate_Title_And_Author()
    {
        // Arrange
        var metadata = new BookMetadata
        {
            Title = "The Pragmatic Programmer",
            Author = "David Thomas"
        };

        _viewModel.NewIsbn = "9780135957059";
        _lookupService
            .LookupByISBNAsync("9780135957059", Arg.Any<CancellationToken>())
            .Returns(metadata);

        // Act
        await _viewModel.LookupByIsbnAsync();

        // Assert
        _viewModel.NewTitle.Should().Be("The Pragmatic Programmer");
        _viewModel.NewAuthor.Should().Be("David Thomas");
        _viewModel.LookupMessage.Should().Be("Book found!");
    }

    [Fact]
    public async Task LookupByIsbnAsync_Should_Set_LookupMessage_When_Not_Found()
    {
        // Arrange
        _viewModel.NewIsbn = "0000000000000";
        _lookupService
            .LookupByISBNAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((BookMetadata?)null);

        // Act
        await _viewModel.LookupByIsbnAsync();

        // Assert
        _viewModel.LookupMessage.Should().Be("No book found for this ISBN.");
        _viewModel.LookupPageCount.Should().BeNull();
        _viewModel.LookupCoverUrl.Should().BeNull();
        _viewModel.LookupDescription.Should().BeNull();
    }

    [Fact]
    public async Task LookupByIsbnAsync_Should_Not_Call_Service_When_Isbn_Is_Empty()
    {
        // Arrange
        _viewModel.NewIsbn = "  ";

        // Act
        await _viewModel.LookupByIsbnAsync();

        // Assert
        await _lookupService.DidNotReceive().LookupByISBNAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddToWishlistAsync_Should_Use_Lookup_Properties_For_New_Book()
    {
        // Arrange
        _viewModel.NewTitle = "Refactoring";
        _viewModel.NewAuthor = "Martin Fowler";
        _viewModel.LookupPageCount = 448;
        _viewModel.LookupCoverUrl = "https://example.com/refactoring.jpg";
        _viewModel.LookupDescription = "Improving the design of existing code.";

        var addedBook = new Book
        {
            Id = Guid.NewGuid(),
            Title = "Refactoring",
            Author = "Martin Fowler",
            Status = ReadingStatus.Wishlist
        };
        _wishlistService
            .AddToWishlistAsync(Arg.Any<Book>(), Arg.Any<WishlistInfo>(), Arg.Any<CancellationToken>())
            .Returns(addedBook);

        // Act
        await _viewModel.AddToWishlistAsync();

        // Assert – the book passed to the service carries the lookup-property values
        await _wishlistService.Received(1).AddToWishlistAsync(
            Arg.Is<Book>(b =>
                b.PageCount == 448 &&
                b.CoverImagePath == "https://example.com/refactoring.jpg" &&
                b.Description == "Improving the design of existing code."),
            Arg.Any<WishlistInfo>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearAddForm_Should_Reset_Lookup_Properties_And_Fire_PropertyChanged()
    {
        // Arrange – set values first
        _viewModel.LookupPageCount = 300;
        _viewModel.LookupCoverUrl = "https://example.com/cover.jpg";
        _viewModel.LookupDescription = "Some description.";

        var changedProperties = new List<string>();
        _viewModel.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act
        _viewModel.ClearAddForm();

        // Assert – properties are null
        _viewModel.LookupPageCount.Should().BeNull();
        _viewModel.LookupCoverUrl.Should().BeNull();
        _viewModel.LookupDescription.Should().BeNull();

        // Assert – INotifyPropertyChanged fired
        changedProperties.Should().Contain("LookupPageCount");
        changedProperties.Should().Contain("LookupCoverUrl");
        changedProperties.Should().Contain("LookupDescription");
    }

    [Fact]
    public async Task LoadAsync_Should_Populate_WishlistBooks()
    {
        // Arrange
        var books = new List<Book>
        {
            new() { Id = Guid.NewGuid(), Title = "Book A", Author = "Author A", Status = ReadingStatus.Wishlist },
            new() { Id = Guid.NewGuid(), Title = "Book B", Author = "Author B", Status = ReadingStatus.Wishlist }
        };
        _wishlistService.GetWishlistBooksAsync(Arg.Any<CancellationToken>()).Returns(books);

        // Act
        await _viewModel.LoadAsync();

        // Assert
        _viewModel.WishlistBooks.Should().HaveCount(2);
        _viewModel.WishlistCount.Should().Be(2);
    }

    [Fact]
    public async Task RemoveFromWishlistAsync_Should_Remove_Book_From_Collection()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        var book = new Book { Id = bookId, Title = "To Remove", Author = "Author", Status = ReadingStatus.Wishlist };
        _viewModel.WishlistBooks.Add(book);
        _wishlistService.RemoveFromWishlistAsync(bookId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Act
        await _viewModel.RemoveFromWishlistAsync(bookId);

        // Assert
        _viewModel.WishlistBooks.Should().BeEmpty();
        _viewModel.WishlistCount.Should().Be(0);
    }
}
