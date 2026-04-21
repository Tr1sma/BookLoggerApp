using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Infrastructure.Services;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class AnnotationServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AnnotationService _service;

    public AnnotationServiceTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);
        _service = new AnnotationService(_unitOfWork);
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
    public async Task GetAllAsync_WithAnnotations_ReturnsAll()
    {
        var book = await AddBookAsync();
        await _service.AddAsync(new Annotation { BookId = book.Id, Note = "A" });
        await _service.AddAsync(new Annotation { BookId = book.Id, Note = "B" });

        var result = await _service.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsAnnotation()
    {
        var book = await AddBookAsync();
        var added = await _service.AddAsync(new Annotation { BookId = book.Id, Note = "Hello" });

        var result = await _service.GetByIdAsync(added.Id);

        result.Should().NotBeNull();
        result!.Note.Should().Be("Hello");
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
        var annotation = new Annotation { BookId = book.Id, Note = "N", CreatedAt = default };

        var result = await _service.AddAsync(annotation);

        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AddAsync_CreatedAtAlreadySet_KeepsValue()
    {
        var book = await AddBookAsync();
        var preset = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        var annotation = new Annotation { BookId = book.Id, Note = "n", CreatedAt = preset };

        var result = await _service.AddAsync(annotation);

        result.CreatedAt.Should().Be(preset);
    }

    [Fact]
    public async Task UpdateAsync_SetsUpdatedAtToNow()
    {
        var book = await AddBookAsync();
        var added = await _service.AddAsync(new Annotation { BookId = book.Id, Note = "Initial" });

        added.Note = "Updated";
        await _service.UpdateAsync(added);

        var reloaded = await _service.GetByIdAsync(added.Id);
        reloaded!.Note.Should().Be("Updated");
        reloaded.UpdatedAt.Should().NotBeNull();
        reloaded.UpdatedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DeleteAsync_Existing_RemovesAnnotation()
    {
        var book = await AddBookAsync();
        var added = await _service.AddAsync(new Annotation { BookId = book.Id, Note = "Bye" });

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
    public async Task GetAnnotationsByBookAsync_FiltersByBookId()
    {
        var book1 = await AddBookAsync("A");
        var book2 = await AddBookAsync("B");
        await _service.AddAsync(new Annotation { BookId = book1.Id, Note = "b1a1" });
        await _service.AddAsync(new Annotation { BookId = book2.Id, Note = "b2a1" });

        var result = await _service.GetAnnotationsByBookAsync(book1.Id);

        result.Should().HaveCount(1);
        result[0].BookId.Should().Be(book1.Id);
    }

    [Fact]
    public async Task SearchAnnotationsAsync_EmptyQuery_ReturnsAll()
    {
        var book = await AddBookAsync();
        await _service.AddAsync(new Annotation { BookId = book.Id, Note = "A" });
        await _service.AddAsync(new Annotation { BookId = book.Id, Note = "B" });

        var result = await _service.SearchAnnotationsAsync("");

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAnnotationsAsync_WhitespaceQuery_ReturnsAll()
    {
        var book = await AddBookAsync();
        await _service.AddAsync(new Annotation { BookId = book.Id, Note = "foo" });

        var result = await _service.SearchAnnotationsAsync("   ");

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchAnnotationsAsync_MatchesNoteCaseInsensitive()
    {
        var book = await AddBookAsync();
        await _service.AddAsync(new Annotation { BookId = book.Id, Note = "Deep thought" });
        await _service.AddAsync(new Annotation { BookId = book.Id, Note = "Trivial" });

        var result = await _service.SearchAnnotationsAsync("DEEP");

        result.Should().HaveCount(1);
        result[0].Note.Should().Be("Deep thought");
    }

    [Fact]
    public async Task SearchAnnotationsAsync_MatchesTitleCaseInsensitive()
    {
        var book = await AddBookAsync();
        await _service.AddAsync(new Annotation { BookId = book.Id, Note = "content", Title = "Chapter Seven" });
        await _service.AddAsync(new Annotation { BookId = book.Id, Note = "content", Title = "Prologue" });

        var result = await _service.SearchAnnotationsAsync("chapter");

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Chapter Seven");
    }

    [Fact]
    public async Task SearchAnnotationsAsync_NullTitleNotMatched()
    {
        var book = await AddBookAsync();
        await _service.AddAsync(new Annotation { BookId = book.Id, Note = "xyz", Title = null });

        var result = await _service.SearchAnnotationsAsync("abc");

        result.Should().BeEmpty();
    }
}
