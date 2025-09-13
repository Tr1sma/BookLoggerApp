using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.ViewModels;

public class BookDetailViewModelTests
{
    [Fact]
    public async Task LoadAsync_Loads_Book_And_Total()
    {
        var books = new InMemoryBookService();
        var progress = new InMemoryProgressService();

        var b = await books.AddAsync(new Book { Title = "VM Test" });
        await progress.AddSessionAsync(new ReadingSession { BookId = b.Id, Minutes = 30 });

        var vm = new BookDetailViewModel(books, progress);
        await vm.LoadAsync(b.Id);

        vm.Book.Should().NotBeNull();
        vm.TotalMinutes.Should().Be(30);
    }

    [Fact]
    public async Task AddSessionAsync_Increases_TotalMinutes()
    {
        var books = new InMemoryBookService();
        var progress = new InMemoryProgressService();

        var b = await books.AddAsync(new Book { Title = "VM Test 2" });

        var vm = new BookDetailViewModel(books, progress);
        await vm.LoadAsync(b.Id);

        await vm.AddSessionAsync(25);
        vm.TotalMinutes.Should().Be(25);

        await vm.AddSessionAsync(10);
        vm.TotalMinutes.Should().Be(35);
    }

    [Fact]
    public async Task AddSessionAsync_Ignores_NonPositive_Minutes()
    {
        var books = new InMemoryBookService();
        var progress = new InMemoryProgressService();

        var b = await books.AddAsync(new Book { Title = "VM Edge" });
        var vm = new BookDetailViewModel(books, progress);
        await vm.LoadAsync(b.Id);

        await vm.AddSessionAsync(0);
        await vm.AddSessionAsync(-5);

        vm.TotalMinutes.Should().Be(0);
    }
}
