using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Repositories;

public class RepositoryTests
{
    [Fact]
    public async Task FirstOrDefaultAsync_ShouldReturnFirstMatch()
    {

        using var context = TestDbContext.Create();
        var repository = new Repository<Book>(context);

        await repository.AddAsync(new Book { Title = "Book A", Author = "Author 1" });
        await repository.AddAsync(new Book { Title = "Book B", Author = "Author 2" });
        await repository.AddAsync(new Book { Title = "Book C", Author = "Author 1" });
        await context.SaveChangesAsync();


        var result = await repository.FirstOrDefaultAsync(b => b.Author == "Author 1");


        result.Should().NotBeNull();
        result!.Title.Should().Be("Book A");
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WhenNoMatch_ShouldReturnNull()
    {

        using var context = TestDbContext.Create();
        var repository = new Repository<Book>(context);

        await repository.AddAsync(new Book { Title = "Book A", Author = "Author 1" });
        await context.SaveChangesAsync();


        var result = await repository.FirstOrDefaultAsync(b => b.Author == "NonExistent");


        result.Should().BeNull();
    }

    [Fact]
    public async Task AddRangeAsync_ShouldAddMultipleEntities()
    {

        using var context = TestDbContext.Create();
        var repository = new Repository<Book>(context);

        var books = new List<Book>
        {
            new Book { Title = "Book 1", Author = "Author 1" },
            new Book { Title = "Book 2", Author = "Author 2" },
            new Book { Title = "Book 3", Author = "Author 3" }
        };


        await repository.AddRangeAsync(books);
        await context.SaveChangesAsync();


        var allBooks = await repository.GetAllAsync();
        allBooks.Should().HaveCount(3);
    }

    [Fact]
    public async Task DeleteRangeAsync_ShouldDeleteMultipleEntities()
    {

        using var context = TestDbContext.Create();
        var repository = new Repository<Book>(context);

        var books = new List<Book>
        {
            new Book { Title = "Book 1", Author = "Author 1" },
            new Book { Title = "Book 2", Author = "Author 2" },
            new Book { Title = "Book 3", Author = "Author 3" }
        };

        await repository.AddRangeAsync(books);
        await context.SaveChangesAsync();

        // Clear the change tracker to avoid tracking conflicts
        context.ChangeTracker.Clear();


        var booksToDelete = await repository.FindAsync(b => b.Author == "Author 1" || b.Author == "Author 2");
        await repository.DeleteRangeAsync(booksToDelete);
        await context.SaveChangesAsync();


        var remainingBooks = await repository.GetAllAsync();
        remainingBooks.Should().HaveCount(1);
        remainingBooks[0].Title.Should().Be("Book 3");
    }

    [Fact]
    public async Task ExistsAsync_WhenEntityExists_ShouldReturnTrue()
    {

        using var context = TestDbContext.Create();
        var repository = new Repository<Book>(context);

        await repository.AddAsync(new Book { Title = "Test Book", Author = "Test Author" });
        await context.SaveChangesAsync();


        var exists = await repository.ExistsAsync(b => b.Title == "Test Book");


        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenEntityDoesNotExist_ShouldReturnFalse()
    {

        using var context = TestDbContext.Create();
        var repository = new Repository<Book>(context);

        await repository.AddAsync(new Book { Title = "Test Book", Author = "Test Author" });
        await context.SaveChangesAsync();


        var exists = await repository.ExistsAsync(b => b.Title == "NonExistent");


        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnIReadOnlyList()
    {

        using var context = TestDbContext.Create();
        var repository = new Repository<Book>(context);

        await repository.AddAsync(new Book { Title = "Book 1", Author = "Author" });
        await repository.AddAsync(new Book { Title = "Book 2", Author = "Author" });
        await context.SaveChangesAsync();


        var result = await repository.GetAllAsync();


        result.Should().BeAssignableTo<IReadOnlyList<Book>>();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindAsync_ShouldReturnIReadOnlyList()
    {

        using var context = TestDbContext.Create();
        var repository = new Repository<Book>(context);

        await repository.AddAsync(new Book { Title = "Book 1", Author = "Author A" });
        await repository.AddAsync(new Book { Title = "Book 2", Author = "Author B" });
        await repository.AddAsync(new Book { Title = "Book 3", Author = "Author A" });
        await context.SaveChangesAsync();


        var result = await repository.FindAsync(b => b.Author == "Author A");


        result.Should().BeAssignableTo<IReadOnlyList<Book>>();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task CountAsync_WithPredicate_ShouldReturnCorrectCount()
    {

        using var context = TestDbContext.Create();
        var repository = new Repository<Book>(context);

        await repository.AddAsync(new Book { Title = "Book 1", Author = "Author A", Status = ReadingStatus.Reading });
        await repository.AddAsync(new Book { Title = "Book 2", Author = "Author B", Status = ReadingStatus.Completed });
        await repository.AddAsync(new Book { Title = "Book 3", Author = "Author C", Status = ReadingStatus.Reading });
        await context.SaveChangesAsync();


        var count = await repository.CountAsync(b => b.Status == ReadingStatus.Reading);


        count.Should().Be(2);
    }
}
