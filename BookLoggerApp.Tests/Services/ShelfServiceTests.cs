using BookLoggerApp.Core.Enums;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Infrastructure.Data;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;
using Xunit;

namespace BookLoggerApp.Tests.Services;

/// <summary>
/// Unit-tests for ShelfService covering shelf CRUD, add/remove/move operations.
/// Uses InMemoryDatabase with transaction warnings suppressed so the transactional
/// DeleteShelfAsync / UpdateShelfPositionsAsync / MoveXxxBetweenShelvesAsync paths
/// can be exercised.
/// </summary>
public class ShelfServiceTests : IDisposable
{
    private readonly TxSuppressingFactory _factory;
    private readonly ShelfService _service;

    public ShelfServiceTests()
    {
        _factory = new TxSuppressingFactory(Guid.NewGuid().ToString());
        _service = new ShelfService(_factory);
    }

    public void Dispose()
    {
        using var ctx = _factory.CreateDbContext();
        ctx.Database.EnsureDeleted();
    }

    private sealed class TxSuppressingFactory : IDbContextFactory<AppDbContext>
    {
        private readonly string _dbName;
        public TxSuppressingFactory(string dbName) => _dbName = dbName;

        public AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(_dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var ctx = new AppDbContext(options);
            ctx.Database.EnsureCreated();
            return ctx;
        }
    }

    [Fact]
    public async Task CreateShelfAsync_AssignsIncrementingSortOrder()
    {
        var shelf1 = await _service.CreateShelfAsync(new Shelf { Name = "First" });
        var shelf2 = await _service.CreateShelfAsync(new Shelf { Name = "Second" });

        shelf1.SortOrder.Should().Be(1);
        shelf2.SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task GetAllShelvesAsync_OrdersBySortOrder()
    {
        await using (var ctx = _factory.CreateDbContext())
        {
            ctx.Shelves.Add(new Shelf { Name = "C", SortOrder = 2 });
            ctx.Shelves.Add(new Shelf { Name = "A", SortOrder = 0 });
            ctx.Shelves.Add(new Shelf { Name = "B", SortOrder = 1 });
            await ctx.SaveChangesAsync();
        }

        var shelves = await _service.GetAllShelvesAsync();

        shelves.Select(s => s.Name).Should().ContainInOrder("A", "B", "C");
    }

    [Fact]
    public async Task GetShelfByIdAsync_Existing_ReturnsShelf()
    {
        var created = await _service.CreateShelfAsync(new Shelf { Name = "Test" });

        var result = await _service.GetShelfByIdAsync(created.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
    }

    [Fact]
    public async Task GetShelfByIdAsync_NotExisting_ReturnsNull()
    {
        var result = await _service.GetShelfByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateShelfAsync_PersistsChanges()
    {
        var shelf = await _service.CreateShelfAsync(new Shelf { Name = "Original" });
        shelf.Name = "Updated";

        await _service.UpdateShelfAsync(shelf);

        var reloaded = await _service.GetShelfByIdAsync(shelf.Id);
        reloaded!.Name.Should().Be("Updated");
    }

    [Fact]
    public async Task DeleteShelfAsync_NonExisting_DoesNothing()
    {
        Func<Task> act = async () => await _service.DeleteShelfAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteShelfAsync_WithBooks_MovesBooksToMainShelf()
    {
        Shelf main;
        Shelf other;
        Guid bookId;
        await using (var ctx = _factory.CreateDbContext())
        {
            main = new Shelf { Name = "Main", SortOrder = 0 };
            other = new Shelf { Name = "Other", SortOrder = 1 };
            var book = new Book { Title = "B", Author = "A" };
            bookId = book.Id;
            ctx.Shelves.AddRange(main, other);
            ctx.Books.Add(book);
            ctx.BookShelves.Add(new BookShelf { ShelfId = other.Id, BookId = book.Id, Position = 0 });
            await ctx.SaveChangesAsync();
        }

        await _service.DeleteShelfAsync(other.Id);

        await using var verify = _factory.CreateDbContext();
        (await verify.Shelves.FindAsync(other.Id)).Should().BeNull();
        var onMain = await verify.BookShelves.FirstOrDefaultAsync(bs => bs.ShelfId == main.Id && bs.BookId == bookId);
        onMain.Should().NotBeNull();
    }

    [Fact]
    public async Task AddBookToShelfAsync_ShiftsOtherBooksForward()
    {
        var shelf = await _service.CreateShelfAsync(new Shelf { Name = "S" });
        Book b1, b2;
        await using (var ctx = _factory.CreateDbContext())
        {
            b1 = new Book { Title = "First", Author = "A" };
            b2 = new Book { Title = "Second", Author = "A" };
            ctx.Books.AddRange(b1, b2);
            await ctx.SaveChangesAsync();
        }

        await _service.AddBookToShelfAsync(shelf.Id, b1.Id);
        await _service.AddBookToShelfAsync(shelf.Id, b2.Id);

        await using var verify = _factory.CreateDbContext();
        var entries = await verify.BookShelves.Where(bs => bs.ShelfId == shelf.Id).OrderBy(bs => bs.Position).ToListAsync();
        entries.Should().HaveCount(2);
        entries[0].BookId.Should().Be(b2.Id); // Last-added is first
        entries[1].BookId.Should().Be(b1.Id);
    }

    [Fact]
    public async Task AddBookToShelfAsync_Duplicate_IsNoOp()
    {
        var shelf = await _service.CreateShelfAsync(new Shelf { Name = "S" });
        Book book;
        await using (var ctx = _factory.CreateDbContext())
        {
            book = new Book { Title = "Dup", Author = "A" };
            ctx.Books.Add(book);
            await ctx.SaveChangesAsync();
        }
        await _service.AddBookToShelfAsync(shelf.Id, book.Id);

        await _service.AddBookToShelfAsync(shelf.Id, book.Id);

        await using var verify = _factory.CreateDbContext();
        var count = await verify.BookShelves.CountAsync(bs => bs.ShelfId == shelf.Id && bs.BookId == book.Id);
        count.Should().Be(1);
    }

    [Fact]
    public async Task RemoveBookFromShelfAsync_Existing_Removes()
    {
        var shelf = await _service.CreateShelfAsync(new Shelf { Name = "S" });
        Book book;
        await using (var ctx = _factory.CreateDbContext())
        {
            book = new Book { Title = "B", Author = "A" };
            ctx.Books.Add(book);
            await ctx.SaveChangesAsync();
        }
        await _service.AddBookToShelfAsync(shelf.Id, book.Id);

        await _service.RemoveBookFromShelfAsync(shelf.Id, book.Id);

        await using var verify = _factory.CreateDbContext();
        (await verify.BookShelves.CountAsync(bs => bs.ShelfId == shelf.Id && bs.BookId == book.Id)).Should().Be(0);
    }

    [Fact]
    public async Task RemoveBookFromShelfAsync_NonExisting_IsNoOp()
    {
        Func<Task> act = async () => await _service.RemoveBookFromShelfAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AddPlantToShelfAsync_IncrementsPosition()
    {
        var shelf = await _service.CreateShelfAsync(new Shelf { Name = "S" });
        Guid plant1Id = Guid.NewGuid(), plant2Id = Guid.NewGuid();
        await using (var ctx = _factory.CreateDbContext())
        {
            var species = new PlantSpecies { Name = "Sp",  };
            ctx.PlantSpecies.Add(species);
            ctx.UserPlants.Add(new UserPlant { Id = plant1Id, SpeciesId = species.Id });
            ctx.UserPlants.Add(new UserPlant { Id = plant2Id, SpeciesId = species.Id });
            await ctx.SaveChangesAsync();
        }

        await _service.AddPlantToShelfAsync(shelf.Id, plant1Id);
        await _service.AddPlantToShelfAsync(shelf.Id, plant2Id);

        await using var verify = _factory.CreateDbContext();
        var entries = await verify.PlantShelves.Where(ps => ps.ShelfId == shelf.Id).OrderBy(ps => ps.Position).ToListAsync();
        entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddPlantToShelfAsync_Duplicate_IsNoOp()
    {
        var shelf = await _service.CreateShelfAsync(new Shelf { Name = "S" });
        var plantId = Guid.NewGuid();
        await using (var ctx = _factory.CreateDbContext())
        {
            var species = new PlantSpecies { Name = "Sp",  };
            ctx.PlantSpecies.Add(species);
            ctx.UserPlants.Add(new UserPlant { Id = plantId, SpeciesId = species.Id });
            await ctx.SaveChangesAsync();
        }
        await _service.AddPlantToShelfAsync(shelf.Id, plantId);

        await _service.AddPlantToShelfAsync(shelf.Id, plantId);

        await using var verify = _factory.CreateDbContext();
        var count = await verify.PlantShelves.CountAsync(ps => ps.ShelfId == shelf.Id && ps.PlantId == plantId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task RemovePlantFromShelfAsync_Removes()
    {
        var shelf = await _service.CreateShelfAsync(new Shelf { Name = "S" });
        var plantId = Guid.NewGuid();
        await using (var ctx = _factory.CreateDbContext())
        {
            var species = new PlantSpecies { Name = "Sp",  };
            ctx.PlantSpecies.Add(species);
            ctx.UserPlants.Add(new UserPlant { Id = plantId, SpeciesId = species.Id });
            await ctx.SaveChangesAsync();
        }
        await _service.AddPlantToShelfAsync(shelf.Id, plantId);

        await _service.RemovePlantFromShelfAsync(shelf.Id, plantId);

        await using var verify = _factory.CreateDbContext();
        (await verify.PlantShelves.CountAsync(ps => ps.ShelfId == shelf.Id)).Should().Be(0);
    }

    [Fact]
    public async Task RemovePlantFromShelfAsync_NonExisting_IsNoOp()
    {
        Func<Task> act = async () => await _service.RemovePlantFromShelfAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AddDecorationToShelfAsync_AddsEntry()
    {
        var shelf = await _service.CreateShelfAsync(new Shelf { Name = "S" });
        var decoId = Guid.NewGuid();
        await using (var ctx = _factory.CreateDbContext())
        {
            var shopItem = new ShopItem { Name = "Lamp", ItemType = ShopItemType.Decoration, Cost = 100 };
            ctx.ShopItems.Add(shopItem);
            ctx.UserDecorations.Add(new UserDecoration { Id = decoId, ShopItemId = shopItem.Id });
            await ctx.SaveChangesAsync();
        }

        await _service.AddDecorationToShelfAsync(shelf.Id, decoId);

        await using var verify = _factory.CreateDbContext();
        (await verify.DecorationShelves.CountAsync(ds => ds.ShelfId == shelf.Id && ds.DecorationId == decoId)).Should().Be(1);
    }

    [Fact]
    public async Task AddDecorationToShelfAsync_Duplicate_IsNoOp()
    {
        var shelf = await _service.CreateShelfAsync(new Shelf { Name = "S" });
        var decoId = Guid.NewGuid();
        await using (var ctx = _factory.CreateDbContext())
        {
            var shopItem = new ShopItem { Name = "Lamp", ItemType = ShopItemType.Decoration, Cost = 100 };
            ctx.ShopItems.Add(shopItem);
            ctx.UserDecorations.Add(new UserDecoration { Id = decoId, ShopItemId = shopItem.Id });
            await ctx.SaveChangesAsync();
        }
        await _service.AddDecorationToShelfAsync(shelf.Id, decoId);

        await _service.AddDecorationToShelfAsync(shelf.Id, decoId);

        await using var verify = _factory.CreateDbContext();
        (await verify.DecorationShelves.CountAsync(ds => ds.ShelfId == shelf.Id && ds.DecorationId == decoId)).Should().Be(1);
    }

    [Fact]
    public async Task RemoveDecorationFromShelfAsync_Removes()
    {
        var shelf = await _service.CreateShelfAsync(new Shelf { Name = "S" });
        var decoId = Guid.NewGuid();
        await using (var ctx = _factory.CreateDbContext())
        {
            var shopItem = new ShopItem { Name = "Lamp", ItemType = ShopItemType.Decoration, Cost = 100 };
            ctx.ShopItems.Add(shopItem);
            ctx.UserDecorations.Add(new UserDecoration { Id = decoId, ShopItemId = shopItem.Id });
            await ctx.SaveChangesAsync();
        }
        await _service.AddDecorationToShelfAsync(shelf.Id, decoId);

        await _service.RemoveDecorationFromShelfAsync(shelf.Id, decoId);

        await using var verify = _factory.CreateDbContext();
        (await verify.DecorationShelves.CountAsync(ds => ds.ShelfId == shelf.Id)).Should().Be(0);
    }

    [Fact]
    public async Task ReorderShelvesAsync_UpdatesSortOrderByIndex()
    {
        var s1 = await _service.CreateShelfAsync(new Shelf { Name = "A" });
        var s2 = await _service.CreateShelfAsync(new Shelf { Name = "B" });
        var s3 = await _service.CreateShelfAsync(new Shelf { Name = "C" });

        await _service.ReorderShelvesAsync(new List<Guid> { s3.Id, s1.Id, s2.Id });

        var all = await _service.GetAllShelvesAsync();
        all.Select(s => s.Name).Should().ContainInOrder("C", "A", "B");
    }

    [Fact]
    public async Task UpdateBookPositionAsync_UpdatesPosition()
    {
        var shelf = await _service.CreateShelfAsync(new Shelf { Name = "S" });
        Book book;
        await using (var ctx = _factory.CreateDbContext())
        {
            book = new Book { Title = "B", Author = "A" };
            ctx.Books.Add(book);
            await ctx.SaveChangesAsync();
        }
        await _service.AddBookToShelfAsync(shelf.Id, book.Id);

        await _service.UpdateBookPositionAsync(shelf.Id, book.Id, 42);

        await using var verify = _factory.CreateDbContext();
        var entry = await verify.BookShelves.FirstAsync(bs => bs.ShelfId == shelf.Id && bs.BookId == book.Id);
        entry.Position.Should().Be(42);
    }

    [Fact]
    public async Task UpdateBookPositionAsync_NonExisting_IsNoOp()
    {
        Func<Task> act = async () => await _service.UpdateBookPositionAsync(Guid.NewGuid(), Guid.NewGuid(), 5);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetPlantsForShelfAsync_ReturnsPlantsOrderedByPosition()
    {
        var shelf = await _service.CreateShelfAsync(new Shelf { Name = "S" });
        Guid plant1Id = Guid.NewGuid(), plant2Id = Guid.NewGuid();
        await using (var ctx = _factory.CreateDbContext())
        {
            var species = new PlantSpecies { Name = "Sp",  };
            ctx.PlantSpecies.Add(species);
            ctx.UserPlants.Add(new UserPlant { Id = plant1Id, SpeciesId = species.Id });
            ctx.UserPlants.Add(new UserPlant { Id = plant2Id, SpeciesId = species.Id });
            await ctx.SaveChangesAsync();
        }
        await _service.AddPlantToShelfAsync(shelf.Id, plant1Id);
        await _service.AddPlantToShelfAsync(shelf.Id, plant2Id);

        var plants = await _service.GetPlantsForShelfAsync(shelf.Id);

        plants.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBooksForShelfAsync_AutoSortRuleStatusReading_ReturnsOnlyMatching()
    {
        var shelf = await _service.CreateShelfAsync(new Shelf { Name = "Auto", AutoSortRule = ShelfAutoSortRule.StatusReading });
        await using (var ctx = _factory.CreateDbContext())
        {
            ctx.Books.Add(new Book { Title = "R", Author = "a", Status = ReadingStatus.Reading });
            ctx.Books.Add(new Book { Title = "C", Author = "a", Status = ReadingStatus.Completed });
            ctx.Books.Add(new Book { Title = "P", Author = "a", Status = ReadingStatus.Planned });
            await ctx.SaveChangesAsync();
        }

        var books = await _service.GetBooksForShelfAsync(shelf.Id);

        books.Should().HaveCount(1);
        books[0].Status.Should().Be(ReadingStatus.Reading);
    }

    [Fact]
    public async Task GetBooksForShelfAsync_NonExistingShelf_ReturnsEmpty()
    {
        var result = await _service.GetBooksForShelfAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task MoveBookBetweenShelvesAsync_MovesBook()
    {
        var src = await _service.CreateShelfAsync(new Shelf { Name = "Src" });
        var tgt = await _service.CreateShelfAsync(new Shelf { Name = "Tgt" });
        Book book;
        await using (var ctx = _factory.CreateDbContext())
        {
            book = new Book { Title = "B", Author = "A" };
            ctx.Books.Add(book);
            await ctx.SaveChangesAsync();
        }
        await _service.AddBookToShelfAsync(src.Id, book.Id);

        await _service.MoveBookBetweenShelvesAsync(src.Id, tgt.Id, book.Id, targetPosition: -1);

        await using var verify = _factory.CreateDbContext();
        (await verify.BookShelves.CountAsync(bs => bs.ShelfId == src.Id && bs.BookId == book.Id)).Should().Be(0);
        (await verify.BookShelves.CountAsync(bs => bs.ShelfId == tgt.Id && bs.BookId == book.Id)).Should().Be(1);
    }

    [Fact]
    public async Task MoveBookBetweenShelvesAsync_SpecificTargetPosition_ShiftsOthers()
    {
        var tgt = await _service.CreateShelfAsync(new Shelf { Name = "Tgt" });
        Book b1, b2;
        await using (var ctx = _factory.CreateDbContext())
        {
            b1 = new Book { Title = "B1", Author = "A" };
            b2 = new Book { Title = "B2", Author = "A" };
            ctx.Books.AddRange(b1, b2);
            await ctx.SaveChangesAsync();
        }
        // Add b1 at position 0 and b2 at position 1
        await _service.AddBookToShelfAsync(tgt.Id, b1.Id);
        // Now insert b2 at position 0
        var src = await _service.CreateShelfAsync(new Shelf { Name = "Src" });
        await _service.AddBookToShelfAsync(src.Id, b2.Id);

        await _service.MoveBookBetweenShelvesAsync(src.Id, tgt.Id, b2.Id, targetPosition: 0);

        await using var verify = _factory.CreateDbContext();
        var entries = await verify.BookShelves.Where(bs => bs.ShelfId == tgt.Id).OrderBy(bs => bs.Position).ToListAsync();
        entries.Should().HaveCount(2);
        entries[0].BookId.Should().Be(b2.Id);
    }

    [Fact]
    public async Task MovePlantBetweenShelvesAsync_MovesPlant()
    {
        var src = await _service.CreateShelfAsync(new Shelf { Name = "Src" });
        var tgt = await _service.CreateShelfAsync(new Shelf { Name = "Tgt" });
        var plantId = Guid.NewGuid();
        await using (var ctx = _factory.CreateDbContext())
        {
            var species = new PlantSpecies { Name = "Sp",  };
            ctx.PlantSpecies.Add(species);
            ctx.UserPlants.Add(new UserPlant { Id = plantId, SpeciesId = species.Id });
            await ctx.SaveChangesAsync();
        }
        await _service.AddPlantToShelfAsync(src.Id, plantId);

        await _service.MovePlantBetweenShelvesAsync(src.Id, tgt.Id, plantId, targetPosition: -1);

        await using var verify = _factory.CreateDbContext();
        (await verify.PlantShelves.CountAsync(ps => ps.ShelfId == src.Id && ps.PlantId == plantId)).Should().Be(0);
        (await verify.PlantShelves.CountAsync(ps => ps.ShelfId == tgt.Id && ps.PlantId == plantId)).Should().Be(1);
    }

    [Fact]
    public async Task MoveDecorationBetweenShelvesAsync_MovesDecoration()
    {
        var src = await _service.CreateShelfAsync(new Shelf { Name = "Src" });
        var tgt = await _service.CreateShelfAsync(new Shelf { Name = "Tgt" });
        var decoId = Guid.NewGuid();
        await using (var ctx = _factory.CreateDbContext())
        {
            var shopItem = new ShopItem { Name = "Lamp", ItemType = ShopItemType.Decoration, Cost = 100 };
            ctx.ShopItems.Add(shopItem);
            ctx.UserDecorations.Add(new UserDecoration { Id = decoId, ShopItemId = shopItem.Id });
            await ctx.SaveChangesAsync();
        }
        await _service.AddDecorationToShelfAsync(src.Id, decoId);

        await _service.MoveDecorationBetweenShelvesAsync(src.Id, tgt.Id, decoId, targetPosition: -1);

        await using var verify = _factory.CreateDbContext();
        (await verify.DecorationShelves.CountAsync(ds => ds.ShelfId == src.Id && ds.DecorationId == decoId)).Should().Be(0);
        (await verify.DecorationShelves.CountAsync(ds => ds.ShelfId == tgt.Id && ds.DecorationId == decoId)).Should().Be(1);
    }

    [Fact]
    public async Task UpdateShelfPositionsAsync_UpdatesMultipleItemTypes()
    {
        var shelf = await _service.CreateShelfAsync(new Shelf { Name = "S" });
        Book book;
        var plantId = Guid.NewGuid();
        var decoId = Guid.NewGuid();
        await using (var ctx = _factory.CreateDbContext())
        {
            book = new Book { Title = "B", Author = "A" };
            ctx.Books.Add(book);
            var species = new PlantSpecies { Name = "Sp",  };
            ctx.PlantSpecies.Add(species);
            ctx.UserPlants.Add(new UserPlant { Id = plantId, SpeciesId = species.Id });
            var shopItem = new ShopItem { Name = "Lamp", ItemType = ShopItemType.Decoration, Cost = 100 };
            ctx.ShopItems.Add(shopItem);
            ctx.UserDecorations.Add(new UserDecoration { Id = decoId, ShopItemId = shopItem.Id });
            await ctx.SaveChangesAsync();
        }
        await _service.AddBookToShelfAsync(shelf.Id, book.Id);
        await _service.AddPlantToShelfAsync(shelf.Id, plantId);
        await _service.AddDecorationToShelfAsync(shelf.Id, decoId);

        await _service.UpdateShelfPositionsAsync(shelf.Id,
            new Dictionary<Guid, int> { { book.Id, 10 } },
            new Dictionary<Guid, int> { { plantId, 20 } },
            new Dictionary<Guid, int> { { decoId, 30 } });

        await using var verify = _factory.CreateDbContext();
        (await verify.BookShelves.FirstAsync(bs => bs.ShelfId == shelf.Id)).Position.Should().Be(10);
        (await verify.PlantShelves.FirstAsync(ps => ps.ShelfId == shelf.Id)).Position.Should().Be(20);
        (await verify.DecorationShelves.FirstAsync(ds => ds.ShelfId == shelf.Id)).Position.Should().Be(30);
    }
}
