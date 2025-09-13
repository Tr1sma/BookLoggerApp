using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services;
using FluentAssertions;
using Xunit;
using static BookLoggerApp.Tests.TestHelpers.TestDb;

namespace BookLoggerApp.Tests.Services;

public class BookServiceTests
{
    [Fact]
    public async Task Add_And_GetById_Works()
    {
        var path = NewPath();
        try
        {
            var svc = new SqliteBookService(path);
            await svc.InitializeAsync();

            var added = await svc.AddAsync(new Book { Title = "Test", Author = "Me" });
            var got = await svc.GetByIdAsync(added.Id);

            got.Should().NotBeNull();
            got!.Title.Should().Be("Test");
            got.Author.Should().Be("Me");
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public async Task Update_Persists_Changes()
    {
        var path = NewPath();
        try
        {
            var svc = new SqliteBookService(path);
            await svc.InitializeAsync();

            var b = await svc.AddAsync(new Book { Title = "A", Author = "X" });
            b.Title = "A (updated)";
            b.Status = ReadingStatus.Reading;

            await svc.UpdateAsync(b);

            var reread = await svc.GetByIdAsync(b.Id);
            reread!.Title.Should().Be("A (updated)");
            reread.Status.Should().Be(ReadingStatus.Reading);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public async Task Delete_Removes_Entity()
    {
        var path = NewPath();
        try
        {
            var svc = new SqliteBookService(path);
            await svc.InitializeAsync();

            var b = await svc.AddAsync(new Book { Title = "ToDelete" });
            await svc.DeleteAsync(b.Id);

            var after = await svc.GetByIdAsync(b.Id);
            after.Should().BeNull();
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public async Task GetAll_Returns_Sorted_By_Title()
    {
        var path = NewPath();
        try
        {
            var svc = new SqliteBookService(path);
            await svc.InitializeAsync();

            await svc.AddAsync(new Book { Title = "Z" });
            await svc.AddAsync(new Book { Title = "M" });
            await svc.AddAsync(new Book { Title = "A" });

            var all = await svc.GetAllAsync();
            all.Select(b => b.Title).Should().ContainInOrder("A", "M", "Z");
        }
        finally { TryDelete(path); }
    }
}
