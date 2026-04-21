using BookLoggerApp.Core.Exceptions;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class QuoteServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly QuoteService _service;

    public QuoteServiceTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);
        _service = new QuoteService(_unitOfWork);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private async Task<Book> AddBookAsync(string title = "Test Book")
    {
        var book = new Book { Title = title, Author = "Test Author" };
        _context.Books.Add(book);
        await _context.SaveChangesAsync();
        return book;
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyList()
    {
        var result = await _service.GetAllAsync();

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WithQuotes_ReturnsAll()
    {
        var book = await AddBookAsync();
        await _service.AddAsync(new Quote { BookId = book.Id, Text = "Quote A" });
        await _service.AddAsync(new Quote { BookId = book.Id, Text = "Quote B" });

        var result = await _service.GetAllAsync();

        result.Should().HaveCount(2);
        result.Select(q => q.Text).Should().Contain(new[] { "Quote A", "Quote B" });
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsQuote()
    {
        var book = await AddBookAsync();
        var added = await _service.AddAsync(new Quote { BookId = book.Id, Text = "Hello" });

        var result = await _service.GetByIdAsync(added.Id);

        result.Should().NotBeNull();
        result!.Text.Should().Be("Hello");
    }

    [Fact]
    public async Task GetByIdAsync_NotExisting_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_CreatedAtDefault_SetsToNow()
    {
        var book = await AddBookAsync();
        var quote = new Quote { BookId = book.Id, Text = "New", CreatedAt = default };

        var result = await _service.AddAsync(quote);

        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AddAsync_CreatedAtAlreadySet_KeepsOriginalValue()
    {
        var book = await AddBookAsync();
        var preset = new DateTime(2023, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var quote = new Quote { BookId = book.Id, Text = "Preset", CreatedAt = preset };

        var result = await _service.AddAsync(quote);

        result.CreatedAt.Should().Be(preset);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var book = await AddBookAsync();
        var added = await _service.AddAsync(new Quote { BookId = book.Id, Text = "Initial" });

        added.Text = "Updated";
        await _service.UpdateAsync(added);

        var reloaded = await _service.GetByIdAsync(added.Id);
        reloaded!.Text.Should().Be("Updated");
    }

    [Fact]
    public async Task DeleteAsync_Existing_RemovesQuote()
    {
        var book = await AddBookAsync();
        var added = await _service.AddAsync(new Quote { BookId = book.Id, Text = "Bye" });

        await _service.DeleteAsync(added.Id);

        (await _service.GetByIdAsync(added.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NotExisting_IsNoOp()
    {
        Func<Task> act = async () => await _service.DeleteAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetQuotesByBookAsync_FiltersByBookId()
    {
        var book1 = await AddBookAsync("A");
        var book2 = await AddBookAsync("B");
        await _service.AddAsync(new Quote { BookId = book1.Id, Text = "b1q1" });
        await _service.AddAsync(new Quote { BookId = book1.Id, Text = "b1q2" });
        await _service.AddAsync(new Quote { BookId = book2.Id, Text = "b2q1" });

        var result = await _service.GetQuotesByBookAsync(book1.Id);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(q => q.BookId == book1.Id);
    }

    [Fact]
    public async Task GetFavoriteQuotesAsync_ReturnsOnlyFavorites()
    {
        var book = await AddBookAsync();
        await _service.AddAsync(new Quote { BookId = book.Id, Text = "fav", IsFavorite = true });
        await _service.AddAsync(new Quote { BookId = book.Id, Text = "not", IsFavorite = false });

        var result = await _service.GetFavoriteQuotesAsync();

        result.Should().HaveCount(1);
        result[0].Text.Should().Be("fav");
    }

    [Fact]
    public async Task SearchQuotesAsync_EmptyQuery_ReturnsAll()
    {
        var book = await AddBookAsync();
        await _service.AddAsync(new Quote { BookId = book.Id, Text = "Alpha" });
        await _service.AddAsync(new Quote { BookId = book.Id, Text = "Beta" });

        var result = await _service.SearchQuotesAsync("");

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchQuotesAsync_WhitespaceQuery_ReturnsAll()
    {
        var book = await AddBookAsync();
        await _service.AddAsync(new Quote { BookId = book.Id, Text = "Alpha" });

        var result = await _service.SearchQuotesAsync("   ");

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchQuotesAsync_CaseInsensitiveMatch()
    {
        var book = await AddBookAsync();
        await _service.AddAsync(new Quote { BookId = book.Id, Text = "Hello World" });
        await _service.AddAsync(new Quote { BookId = book.Id, Text = "Goodbye" });

        var result = await _service.SearchQuotesAsync("WORLD");

        result.Should().HaveCount(1);
        result[0].Text.Should().Be("Hello World");
    }

    [Fact]
    public async Task ToggleFavoriteAsync_FlipsValue()
    {
        var book = await AddBookAsync();
        var added = await _service.AddAsync(new Quote { BookId = book.Id, Text = "q", IsFavorite = false });

        await _service.ToggleFavoriteAsync(added.Id);

        var reloaded = await _service.GetByIdAsync(added.Id);
        reloaded!.IsFavorite.Should().BeTrue();

        await _service.ToggleFavoriteAsync(added.Id);
        reloaded = await _service.GetByIdAsync(added.Id);
        reloaded!.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleFavoriteAsync_NotExisting_ThrowsEntityNotFound()
    {
        Func<Task> act = async () => await _service.ToggleFavoriteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
