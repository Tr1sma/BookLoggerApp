using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Repositories;

/// <summary>
/// Tests for the generic Repository&lt;T&gt; class via a representative entity (Genre).
/// Covers paths not exercised by specific-repository tests: CountAsync, CountAsync(predicate),
/// ExistsAsync, FirstOrDefaultAsync, UpdateAsync with tracked/detached entities,
/// DeleteRangeAsync, AddRangeAsync.
/// </summary>
public class GenericRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Repository<Genre> _repository;

    public GenericRepositoryTests()
    {
        _context = TestDbContext.Create();
        _repository = new Repository<Genre>(_context);
        // Clear any seeded genres for predictable assertions
        _context.Genres.RemoveRange(_context.Genres);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsEntity()
    {
        var genre = new Genre { Name = "SF" };
        await _repository.AddAsync(genre);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByIdAsync(genre.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("SF");
    }

    [Fact]
    public async Task GetByIdAsync_NotExisting_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntities()
    {
        await _repository.AddAsync(new Genre { Name = "A" });
        await _repository.AddAsync(new Genre { Name = "B" });
        await _context.SaveChangesAsync();

        var result = await _repository.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindAsync_AppliesPredicate()
    {
        await _repository.AddAsync(new Genre { Name = "Fantasy" });
        await _repository.AddAsync(new Genre { Name = "Mystery" });
        await _context.SaveChangesAsync();

        var result = await _repository.FindAsync(g => g.Name.StartsWith("F"));

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Fantasy");
    }

    [Fact]
    public async Task FirstOrDefaultAsync_Match_ReturnsEntity()
    {
        await _repository.AddAsync(new Genre { Name = "Romance" });
        await _context.SaveChangesAsync();

        var result = await _repository.FirstOrDefaultAsync(g => g.Name == "Romance");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task FirstOrDefaultAsync_NoMatch_ReturnsNull()
    {
        var result = await _repository.FirstOrDefaultAsync(g => g.Name == "Nope");

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_AddsEntity()
    {
        var genre = new Genre { Name = "Horror" };

        var result = await _repository.AddAsync(genre);
        await _context.SaveChangesAsync();

        result.Should().Be(genre);
        (await _repository.GetByIdAsync(genre.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task AddRangeAsync_AddsMultiple()
    {
        var genres = new[]
        {
            new Genre { Name = "A" },
            new Genre { Name = "B" },
            new Genre { Name = "C" }
        };

        await _repository.AddRangeAsync(genres);
        await _context.SaveChangesAsync();

        (await _repository.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task CountAsync_Total()
    {
        await _repository.AddAsync(new Genre { Name = "A" });
        await _repository.AddAsync(new Genre { Name = "B" });
        await _context.SaveChangesAsync();

        (await _repository.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task CountAsync_WithPredicate()
    {
        await _repository.AddAsync(new Genre { Name = "Alpha" });
        await _repository.AddAsync(new Genre { Name = "Beta" });
        await _repository.AddAsync(new Genre { Name = "Alphabet" });
        await _context.SaveChangesAsync();

        var count = await _repository.CountAsync(g => g.Name.StartsWith("Alpha"));

        count.Should().Be(2);
    }

    [Fact]
    public async Task ExistsAsync_Match_ReturnsTrue()
    {
        await _repository.AddAsync(new Genre { Name = "Exists" });
        await _context.SaveChangesAsync();

        (await _repository.ExistsAsync(g => g.Name == "Exists")).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NoMatch_ReturnsFalse()
    {
        (await _repository.ExistsAsync(g => g.Name == "NoSuch")).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntity()
    {
        var genre = new Genre { Name = "Gone" };
        await _repository.AddAsync(genre);
        await _context.SaveChangesAsync();

        await _repository.DeleteAsync(genre);
        await _context.SaveChangesAsync();

        (await _repository.GetByIdAsync(genre.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteRangeAsync_RemovesMultiple()
    {
        var g1 = new Genre { Name = "A" };
        var g2 = new Genre { Name = "B" };
        await _repository.AddAsync(g1);
        await _repository.AddAsync(g2);
        await _context.SaveChangesAsync();

        await _repository.DeleteRangeAsync(new[] { g1, g2 });
        await _context.SaveChangesAsync();

        (await _repository.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_DetachedEntity_ReplacesTracked()
    {
        // Attach an entity, save, then work with a detached copy with same ID
        var original = new Genre { Name = "Original" };
        await _repository.AddAsync(original);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var detached = new Genre { Id = original.Id, Name = "Updated" };
        await _repository.UpdateAsync(detached);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var reloaded = await _repository.GetByIdAsync(original.Id);
        reloaded!.Name.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdateAsync_TrackedEntity_UpdatesProperties()
    {
        var genre = new Genre { Name = "TrackedOriginal" };
        await _repository.AddAsync(genre);
        await _context.SaveChangesAsync();

        genre.Name = "TrackedChanged";
        await _repository.UpdateAsync(genre);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var reloaded = await _repository.GetByIdAsync(genre.Id);
        reloaded!.Name.Should().Be("TrackedChanged");
    }

    [Fact]
    public async Task UpdateAsync_DetachedConflictsWithTracked_ReplacesTracked()
    {
        // Arrange: load a tracked instance, then present a different detached instance with same key
        var genre = new Genre { Name = "First" };
        await _repository.AddAsync(genre);
        await _context.SaveChangesAsync();

        // First access loads into tracking
        var trackedLoad = await _repository.GetByIdAsync(genre.Id);
        trackedLoad.Should().NotBeNull();

        // Manually attach a fresh instance with same ID
        var detached = new Genre { Id = genre.Id, Name = "Replacement" };
        await _repository.UpdateAsync(detached);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var final = await _repository.GetByIdAsync(genre.Id);
        final!.Name.Should().Be("Replacement");
    }
}
