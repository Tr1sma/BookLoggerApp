using System.Net;
using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Infrastructure;
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
    private readonly WishlistViewModel _vm;

    public WishlistViewModelTests()
    {
        DatabaseInitializationHelper.MarkAsInitialized();
        _wishlistService = Substitute.For<IWishlistService>();
        _lookupService = Substitute.For<ILookupService>();

        _wishlistService.GetWishlistBooksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Book>>(new List<Book>()));
        _wishlistService.SearchWishlistAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Book>>(new List<Book>()));

        _vm = new WishlistViewModel(_wishlistService, _lookupService);
    }

    [Fact]
    public async Task LoadAsync_PopulatesCollection()
    {
        _wishlistService.GetWishlistBooksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Book>>(new List<Book>
            {
                new() { Title = "A", Author = "X" },
                new() { Title = "B", Author = "Y" }
            }));

        await _vm.LoadCommand.ExecuteAsync(null);

        _vm.WishlistBooks.Should().HaveCount(2);
        _vm.WishlistCount.Should().Be(2);
    }

    [Fact]
    public async Task AddToWishlistAsync_CreatesBookAndClearsForm()
    {
        _vm.NewTitle = "  Dune  ";
        _vm.NewAuthor = "Frank";
        _vm.NewIsbn = "9781234567890";
        _vm.NewPriority = WishlistPriority.High;
        _vm.NewRecommendedBy = "Friend";
        _vm.NewWishlistNotes = "Must read";

        var returned = new Book { Id = Guid.NewGuid(), Title = "Dune" };
        _wishlistService.AddToWishlistAsync(Arg.Any<Book>(), Arg.Any<WishlistInfo>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(returned));

        await _vm.AddToWishlistCommand.ExecuteAsync(null);

        await _wishlistService.Received(1).AddToWishlistAsync(
            Arg.Is<Book>(b => b.Title == "Dune" && b.Author == "Frank" && b.ISBN == "9781234567890"),
            Arg.Is<WishlistInfo>(i => i.Priority == WishlistPriority.High && i.RecommendedBy == "Friend"),
            Arg.Any<CancellationToken>());
        _vm.WishlistBooks.Should().Contain(returned);
        _vm.NewTitle.Should().BeEmpty();
        _vm.NewAuthor.Should().BeEmpty();
        _vm.NewPriority.Should().Be(WishlistPriority.Medium);
    }

    [Fact]
    public async Task AddToWishlistAsync_BlankIsbn_PassesNull()
    {
        _vm.NewTitle = "NoIsbn";
        _vm.NewAuthor = "A";
        _vm.NewIsbn = "";
        _wishlistService.AddToWishlistAsync(Arg.Any<Book>(), Arg.Any<WishlistInfo>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult((Book)ci[0]));

        await _vm.AddToWishlistCommand.ExecuteAsync(null);

        await _wishlistService.Received(1).AddToWishlistAsync(
            Arg.Is<Book>(b => b.ISBN == null),
            Arg.Any<WishlistInfo>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LookupByIsbnAsync_EmptyIsbn_DoesNothing()
    {
        _vm.NewIsbn = "";

        await _vm.LookupByIsbnCommand.ExecuteAsync(null);

        await _lookupService.DidNotReceive().LookupByISBNAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LookupByIsbnAsync_Success_FillsFields()
    {
        _vm.NewIsbn = "9780140449136";
        _lookupService.LookupByISBNAsync("9780140449136", Arg.Any<CancellationToken>()).Returns(Task.FromResult<BookMetadata?>(new BookMetadata
        {
            Title = "The Odyssey",
            Author = "Homer",
            PageCount = 500,
            CoverImageUrl = "https://example.com/img.jpg",
            Description = "Epic"
        }));

        await _vm.LookupByIsbnCommand.ExecuteAsync(null);

        _vm.NewTitle.Should().Be("The Odyssey");
        _vm.NewAuthor.Should().Be("Homer");
        _vm.LookupMessage.Should().Be("Book found!");
        _vm.IsLookingUp.Should().BeFalse();
    }

    [Fact]
    public async Task LookupByIsbnAsync_NotFound_SetsNotFoundMessage()
    {
        _vm.NewIsbn = "0000000000";
        _lookupService.LookupByISBNAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<BookMetadata?>(null));

        await _vm.LookupByIsbnCommand.ExecuteAsync(null);

        _vm.LookupMessage.Should().Contain("No book found");
    }

    [Fact]
    public async Task LookupByIsbnAsync_QuotaExceeded_SetsQuotaMessage()
    {
        _vm.NewIsbn = "9780140449136";
        _lookupService.LookupByISBNAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<BookMetadata?>>(_ => throw new HttpRequestException("429 rateLimitExceeded", null, HttpStatusCode.TooManyRequests));

        await _vm.LookupByIsbnCommand.ExecuteAsync(null);

        _vm.LookupMessage.Should().Contain("quota");
    }

    [Fact]
    public async Task LookupByIsbnAsync_TaskCanceled_SetsTimeoutMessage()
    {
        _vm.NewIsbn = "9780140449136";
        _lookupService.LookupByISBNAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<BookMetadata?>>(_ => throw new TaskCanceledException());

        await _vm.LookupByIsbnCommand.ExecuteAsync(null);

        _vm.LookupMessage.Should().Contain("timed out");
    }

    [Fact]
    public async Task LookupByIsbnAsync_GenericException_SetsGenericMessage()
    {
        _vm.NewIsbn = "9780140449136";
        _lookupService.LookupByISBNAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<BookMetadata?>>(_ => throw new InvalidOperationException("boom"));

        await _vm.LookupByIsbnCommand.ExecuteAsync(null);

        _vm.LookupMessage.Should().Contain("boom");
    }

    [Fact]
    public async Task LookupByIsbnAsync_HttpError_SetsHttpMessage()
    {
        _vm.NewIsbn = "9780140449136";
        _lookupService.LookupByISBNAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<BookMetadata?>>(_ => throw new HttpRequestException("server err", null, HttpStatusCode.InternalServerError));

        await _vm.LookupByIsbnCommand.ExecuteAsync(null);

        _vm.LookupMessage.Should().Contain("HTTP 500");
    }

    [Fact]
    public async Task MoveToLibraryAsync_RemovesFromCollection()
    {
        var book = new Book { Id = Guid.NewGuid(), Title = "B" };
        _vm.WishlistBooks.Add(book);
        _vm.WishlistCount = 1;

        await _vm.MoveToLibraryCommand.ExecuteAsync(book.Id);

        await _wishlistService.Received(1).MoveToLibraryAsync(book.Id, Arg.Any<CancellationToken>());
        _vm.WishlistBooks.Should().NotContain(book);
        _vm.WishlistCount.Should().Be(0);
    }

    [Fact]
    public async Task RemoveFromWishlistAsync_RemovesFromCollection()
    {
        var book = new Book { Id = Guid.NewGuid(), Title = "B" };
        _vm.WishlistBooks.Add(book);
        _vm.WishlistCount = 1;

        await _vm.RemoveFromWishlistCommand.ExecuteAsync(book.Id);

        await _wishlistService.Received(1).RemoveFromWishlistAsync(book.Id, Arg.Any<CancellationToken>());
        _vm.WishlistBooks.Should().NotContain(book);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_CallsGetAll()
    {
        _vm.SearchQuery = "";

        await _vm.SearchCommand.ExecuteAsync(null);

        await _wishlistService.Received(1).GetWishlistBooksAsync(Arg.Any<CancellationToken>());
        await _wishlistService.DidNotReceive().SearchWishlistAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_WithQuery_CallsSearch()
    {
        _vm.SearchQuery = "dune";
        _wishlistService.SearchWishlistAsync("dune", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Book>>(new List<Book> { new() { Title = "Dune" } }));

        await _vm.SearchCommand.ExecuteAsync(null);

        await _wishlistService.Received(1).SearchWishlistAsync("dune", Arg.Any<CancellationToken>());
        _vm.WishlistBooks.Should().HaveCount(1);
    }

    [Fact]
    public async Task SortAsync_EmptyCollection_IsNoOp()
    {
        await _vm.SortCommand.ExecuteAsync(null);

        _vm.WishlistBooks.Should().BeEmpty();
    }

    [Fact]
    public async Task SortAsync_ByTitle_OrdersAlphabetically()
    {
        _vm.WishlistBooks.Add(new Book { Title = "Zebra", Author = "A" });
        _vm.WishlistBooks.Add(new Book { Title = "Alpha", Author = "A" });
        _vm.SortBy = "Title";

        await _vm.SortCommand.ExecuteAsync(null);

        _vm.WishlistBooks[0].Title.Should().Be("Alpha");
        _vm.WishlistBooks[1].Title.Should().Be("Zebra");
    }

    [Fact]
    public async Task SortAsync_ByAuthor_OrdersAlphabetically()
    {
        _vm.WishlistBooks.Add(new Book { Title = "A", Author = "Zephyr" });
        _vm.WishlistBooks.Add(new Book { Title = "B", Author = "Albert" });
        _vm.SortBy = "Author";

        await _vm.SortCommand.ExecuteAsync(null);

        _vm.WishlistBooks[0].Author.Should().Be("Albert");
    }

    [Fact]
    public async Task SortAsync_ByPriority_OrdersByPriorityDescending()
    {
        _vm.WishlistBooks.Add(new Book { Title = "Low", Author = "A", WishlistInfo = new WishlistInfo { Priority = WishlistPriority.Low } });
        _vm.WishlistBooks.Add(new Book { Title = "High", Author = "A", WishlistInfo = new WishlistInfo { Priority = WishlistPriority.High } });
        _vm.SortBy = "Priority";

        await _vm.SortCommand.ExecuteAsync(null);

        _vm.WishlistBooks[0].Title.Should().Be("High");
    }

    [Fact]
    public void ClearAddForm_ResetsAllFields()
    {
        _vm.NewTitle = "x";
        _vm.NewAuthor = "y";
        _vm.NewIsbn = "z";
        _vm.NewPriority = WishlistPriority.High;
        _vm.NewRecommendedBy = "friend";
        _vm.NewWishlistNotes = "notes";
        _vm.LookupMessage = "msg";

        _vm.ClearAddForm();

        _vm.NewTitle.Should().BeEmpty();
        _vm.NewAuthor.Should().BeEmpty();
        _vm.NewIsbn.Should().BeEmpty();
        _vm.NewPriority.Should().Be(WishlistPriority.Medium);
        _vm.NewRecommendedBy.Should().BeEmpty();
        _vm.NewWishlistNotes.Should().BeEmpty();
        _vm.LookupMessage.Should().BeNull();
    }
}
