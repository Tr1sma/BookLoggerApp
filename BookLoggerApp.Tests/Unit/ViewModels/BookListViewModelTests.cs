using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BookLoggerApp.Tests.Unit.ViewModels;

public class BookListViewModelTests
{
    private readonly IBookService _bookService;
    private readonly BookListViewModel _vm;

    public BookListViewModelTests()
    {
        DatabaseInitializationHelper.MarkAsInitialized();
        _bookService = Substitute.For<IBookService>();
        _bookService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Book>>(new List<Book>()));
        _vm = new BookListViewModel(_bookService);
    }

    [Fact]
    public async Task LoadAsync_PopulatesItemsFromService()
    {
        var books = new List<Book>
        {
            new() { Title = "B1", Author = "A" },
            new() { Title = "B2", Author = "A" }
        };
        _bookService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Book>>(books));

        await _vm.LoadCommand.ExecuteAsync(null);

        _vm.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_ClearsPreviousItemsBeforeAdding()
    {
        _vm.Items.Add(new Book { Title = "Stale", Author = "A" });
        _bookService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Book>>(
            new List<Book> { new() { Title = "Fresh", Author = "A" } }));

        await _vm.LoadCommand.ExecuteAsync(null);

        _vm.Items.Should().HaveCount(1);
        _vm.Items[0].Title.Should().Be("Fresh");
    }

    [Fact]
    public async Task LoadAsync_ServiceThrows_SetsErrorMessage()
    {
        _bookService.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<Book>>>(_ => throw new InvalidOperationException("fail"));

        await _vm.LoadCommand.ExecuteAsync(null);

        _vm.ErrorMessage.Should().NotBeNull();
        _vm.ErrorMessage!.Should().Contain("Failed to load books");
    }

    [Fact]
    public async Task AddAsync_ValidInput_AddsToCollection()
    {
        _vm.NewTitle = "Brand New";
        _vm.NewAuthor = "Somebody";
        _bookService.AddAsync(Arg.Any<Book>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult((Book)ci[0]));

        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)_vm.AddAsyncCommand).ExecuteAsync(null);

        _vm.Items.Should().HaveCount(1);
        _vm.Items[0].Title.Should().Be("Brand New");
        _vm.NewTitle.Should().Be("");
        _vm.NewAuthor.Should().Be("");
    }

    [Fact]
    public void AddCommand_EmptyTitle_CannotExecute()
    {
        _vm.NewTitle = "";

        _vm.AddAsyncCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void AddCommand_WhitespaceTitle_CannotExecute()
    {
        _vm.NewTitle = "   ";

        _vm.AddAsyncCommand.CanExecute(null).Should().BeFalse();
    }
}
