using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services;
using BookLoggerApp.Core.ViewModels;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.ViewModels;

public class BookListViewModelTests
{
    [Fact]
    public async Task LoadAsync_Populates_Items()
    {
        var svc = new InMemoryBookService(); // seeds demo data
        var vm = new BookListViewModel(svc);

        await vm.LoadAsync();

        vm.Items.Should().NotBeEmpty();
        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_Adds_Item_And_Clears_Inputs()
    {
        var svc = new InMemoryBookService();
        var vm = new BookListViewModel(svc)
        {
            NewTitle = "New Book",
            NewAuthor = "Author X"
        };

        (await svc.GetAllAsync()).Count.Should().BeGreaterThanOrEqualTo(0);

        await vm.AddAsync();

        vm.Items.Any(b => b.Title == "New Book").Should().BeTrue();
        vm.NewTitle.Should().BeEmpty();
        vm.NewAuthor.Should().BeEmpty();
        vm.AddAsyncCommand.CanExecute(null).Should().BeFalse(); // since NewTitle cleared
    }
}
