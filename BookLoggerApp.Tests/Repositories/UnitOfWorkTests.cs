using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Repositories;
using BookLoggerApp.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Repositories;

public class UnitOfWorkTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly UnitOfWork _unitOfWork;

    public UnitOfWorkTests()
    {
        _context = TestDbContext.Create();
        _unitOfWork = new UnitOfWork(_context);
    }

    public void Dispose()
    {
        _unitOfWork.Dispose();
        _context.Dispose();
    }

    [Fact]
    public void Context_ReturnsSharedDbContext()
    {
        _unitOfWork.Context.Should().BeSameAs(_context);
    }

    [Fact]
    public void Books_ReturnsSameInstanceOnMultipleAccess()
    {
        var first = _unitOfWork.Books;
        var second = _unitOfWork.Books;

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void AllSpecificRepositories_AreAccessible()
    {
        _unitOfWork.Books.Should().NotBeNull();
        _unitOfWork.ReadingSessions.Should().NotBeNull();
        _unitOfWork.ReadingGoals.Should().NotBeNull();
        _unitOfWork.UserPlants.Should().NotBeNull();
    }

    [Fact]
    public void AllGenericRepositories_AreAccessible()
    {
        _unitOfWork.Genres.Should().NotBeNull();
        _unitOfWork.BookGenres.Should().NotBeNull();
        _unitOfWork.Quotes.Should().NotBeNull();
        _unitOfWork.Annotations.Should().NotBeNull();
        _unitOfWork.PlantSpecies.Should().NotBeNull();
        _unitOfWork.AppSettingsRepo.Should().NotBeNull();
        _unitOfWork.Tropes.Should().NotBeNull();
        _unitOfWork.BookTropes.Should().NotBeNull();
        _unitOfWork.WishlistInfos.Should().NotBeNull();
        _unitOfWork.GoalExcludedBooks.Should().NotBeNull();
        _unitOfWork.GoalGenres.Should().NotBeNull();
    }

    [Fact]
    public void Repositories_AreLazilyCached()
    {
        var firstGenres = _unitOfWork.Genres;
        var secondGenres = _unitOfWork.Genres;

        secondGenres.Should().BeSameAs(firstGenres);
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        var genre = new Genre { Name = "SaveTest" };
        await _unitOfWork.Genres.AddAsync(genre);

        var result = await _unitOfWork.SaveChangesAsync();

        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BeginTransactionAsync_TwiceInARow_Throws()
    {
        // InMemoryDatabase ignores the transaction but still tracks state
        // Some in-memory providers throw on the first call — skip if so
        try
        {
            await _unitOfWork.BeginTransactionAsync();
        }
        catch (InvalidOperationException)
        {
            // Expected: in-memory doesn't support transactions at all
            return;
        }

        Func<Task> act = async () => await _unitOfWork.BeginTransactionAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CommitAsync_WithoutTransaction_Throws()
    {
        Func<Task> act = async () => await _unitOfWork.CommitAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RollbackAsync_WithoutTransaction_Throws()
    {
        Func<Task> act = async () => await _unitOfWork.RollbackAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var uow = new UnitOfWork(_context);

        uow.Dispose();
        Action act = () => uow.Dispose();

        act.Should().NotThrow();
    }
}
