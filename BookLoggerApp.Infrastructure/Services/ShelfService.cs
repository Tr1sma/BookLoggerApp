using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace BookLoggerApp.Infrastructure.Services;

public class ShelfService : IShelfService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ShelfService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Shelf>> GetAllShelvesAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Shelves
            .OrderBy(s => s.SortOrder)
            .ToListAsync();
    }

    public async Task<Shelf?> GetShelfByIdAsync(Guid id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Shelves
            .Include(s => s.BookShelves)
            .ThenInclude(bs => bs.Book)
            .Include(s => s.PlantShelves)
            .ThenInclude(ps => ps.Plant)
            .ThenInclude(p => p.Species)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Shelf> CreateShelfAsync(Shelf shelf)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        // Assign sort order to be last
        var maxSortOrder = await context.Shelves.MaxAsync(s => (int?)s.SortOrder) ?? 0;
        shelf.SortOrder = maxSortOrder + 1;

        context.Shelves.Add(shelf);
        await context.SaveChangesAsync();
        return shelf;
    }

    public async Task UpdateShelfAsync(Shelf shelf)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.Shelves.Update(shelf);
        await context.SaveChangesAsync();
    }

    public async Task DeleteShelfAsync(Guid id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var shelf = await context.Shelves.FindAsync(id);
            if (shelf == null) return;

            // Find or create the Main shelf (SortOrder == 0)
            var mainShelf = await context.Shelves
                .FirstOrDefaultAsync(s => s.SortOrder == 0 && s.Id != id);

            if (mainShelf == null)
            {
                mainShelf = new Shelf { Name = "Main Shelf", SortOrder = 0 };
                context.Shelves.Add(mainShelf);
                await context.SaveChangesAsync();
            }

            // Get current max position on the Main shelf
            var maxPos = Math.Max(
                await context.BookShelves.Where(bs => bs.ShelfId == mainShelf.Id)
                    .MaxAsync(bs => (int?)bs.Position) ?? -1,
                await context.PlantShelves.Where(ps => ps.ShelfId == mainShelf.Id)
                    .MaxAsync(ps => (int?)ps.Position) ?? -1);

            // Move books to Main shelf (skip duplicates already on Main shelf)
            var booksToMove = await context.BookShelves
                .Where(bs => bs.ShelfId == id)
                .ToListAsync();

            var existingBookIds = await context.BookShelves
                .Where(bs => bs.ShelfId == mainShelf.Id)
                .Select(bs => bs.BookId)
                .ToHashSetAsync();

            foreach (var bs in booksToMove)
            {
                context.BookShelves.Remove(bs);
                if (!existingBookIds.Contains(bs.BookId))
                {
                    maxPos++;
                    context.BookShelves.Add(new BookShelf
                    {
                        ShelfId = mainShelf.Id,
                        BookId = bs.BookId,
                        Position = maxPos
                    });
                }
            }

            // Move plants to Main shelf (skip duplicates already on Main shelf)
            var plantsToMove = await context.PlantShelves
                .Where(ps => ps.ShelfId == id)
                .ToListAsync();

            var existingPlantIds = await context.PlantShelves
                .Where(ps => ps.ShelfId == mainShelf.Id)
                .Select(ps => ps.PlantId)
                .ToHashSetAsync();

            foreach (var ps in plantsToMove)
            {
                context.PlantShelves.Remove(ps);
                if (!existingPlantIds.Contains(ps.PlantId))
                {
                    maxPos++;
                    context.PlantShelves.Add(new PlantShelf
                    {
                        ShelfId = mainShelf.Id,
                        PlantId = ps.PlantId,
                        Position = maxPos
                    });
                }
            }

            context.Shelves.Remove(shelf);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task AddBookToShelfAsync(Guid shelfId, Guid bookId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var exists = await context.BookShelves
            .AnyAsync(bs => bs.ShelfId == shelfId && bs.BookId == bookId);

        if (!exists)
        {
            // Shift all existing items forward so the new book goes to position 0 (first)
            var existingEntries = await context.BookShelves
                .Where(bs => bs.ShelfId == shelfId)
                .ToListAsync();

            foreach (var entry in existingEntries)
            {
                entry.Position += 1;
            }

            context.BookShelves.Add(new BookShelf
            {
                ShelfId = shelfId,
                BookId = bookId,
                Position = 0
            });
            await context.SaveChangesAsync();
        }
    }

    public async Task RemoveBookFromShelfAsync(Guid shelfId, Guid bookId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var bookShelf = await context.BookShelves
            .FirstOrDefaultAsync(bs => bs.ShelfId == shelfId && bs.BookId == bookId);

        if (bookShelf != null)
        {
            context.BookShelves.Remove(bookShelf);
            await context.SaveChangesAsync();
        }
    }

    public async Task ReorderShelvesAsync(List<Guid> shelfIdsInOrder)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var shelves = await context.Shelves
            .Where(s => shelfIdsInOrder.Contains(s.Id))
            .ToListAsync();

        foreach (var shelf in shelves)
        {
            var index = shelfIdsInOrder.IndexOf(shelf.Id);
            if (index != -1)
            {
                shelf.SortOrder = index;
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task UpdateBookPositionAsync(Guid shelfId, Guid bookId, int newPosition)
    {
        // This is a bit complex due to reordering. 
        // For now, a simple swap or shift approach is acceptable, but full list reorder is safer.
        // Assuming we might just update the single item for now or handle list reorder logic.
        // Will implement a rigorous re-indexer if drag-and-drop requires it commonly.

        using var context = await _contextFactory.CreateDbContextAsync();
        var targetEntry = await context.BookShelves
            .FirstOrDefaultAsync(bs => bs.ShelfId == shelfId && bs.BookId == bookId);

        if (targetEntry != null)
        {
            targetEntry.Position = newPosition;
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<Book>> GetBooksForShelfAsync(Guid shelfId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var shelf = await context.Shelves.FindAsync(shelfId);
        if (shelf == null) return new List<Book>();

        if (shelf.AutoSortRule != ShelfAutoSortRule.None)
        {
            // Dynamic query based on rule
            var query = context.Books.AsQueryable();
            switch (shelf.AutoSortRule)
            {
                case ShelfAutoSortRule.StatusPlanned:
                    query = query.Where(b => b.Status == ReadingStatus.Planned);
                    break;
                case ShelfAutoSortRule.StatusReading:
                    query = query.Where(b => b.Status == ReadingStatus.Reading);
                    break;
                case ShelfAutoSortRule.StatusCompleted:
                    query = query.Where(b => b.Status == ReadingStatus.Completed);
                    break;
                case ShelfAutoSortRule.StatusAbandoned:
                    query = query.Where(b => b.Status == ReadingStatus.Abandoned);
                    break;
                case ShelfAutoSortRule.StatusWishlist:
                    query = query.Where(b => b.Status == ReadingStatus.Wishlist);
                    break;
            }
            return await query.OrderByDescending(b => b.DateAdded).ToListAsync();
        }
        else
        {
            // Standard manual shelf
            return await context.BookShelves
                .Where(bs => bs.ShelfId == shelfId)
                .OrderBy(bs => bs.Position)
                .Select(bs => bs.Book)
                .ToListAsync();
        }
    }

    public async Task AddPlantToShelfAsync(Guid shelfId, Guid plantId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var exists = await context.PlantShelves
            .AnyAsync(ps => ps.ShelfId == shelfId && ps.PlantId == plantId);

        if (!exists)
        {
            var maxBookPos = await context.BookShelves
                .Where(bs => bs.ShelfId == shelfId)
                .MaxAsync(bs => (int?)bs.Position) ?? -1;

            var maxPlantPos = await context.PlantShelves
                .Where(ps => ps.ShelfId == shelfId)
                .MaxAsync(ps => (int?)ps.Position) ?? -1;

            var maxPos = Math.Max(maxBookPos, maxPlantPos);

            context.PlantShelves.Add(new PlantShelf
            {
                ShelfId = shelfId,
                PlantId = plantId,
                Position = maxPos + 1
            });
            await context.SaveChangesAsync();
        }
    }

    public async Task RemovePlantFromShelfAsync(Guid shelfId, Guid plantId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var plantShelf = await context.PlantShelves
            .FirstOrDefaultAsync(ps => ps.ShelfId == shelfId && ps.PlantId == plantId);

        if (plantShelf != null)
        {
            context.PlantShelves.Remove(plantShelf);
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<UserPlant>> GetPlantsForShelfAsync(Guid shelfId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.PlantShelves
            .Where(ps => ps.ShelfId == shelfId)
            .OrderBy(ps => ps.Position)
            .Select(ps => ps.Plant)
            .ToListAsync();
    }

    public async Task UpdateShelfPositionsAsync(Guid shelfId, Dictionary<Guid, int> bookPositions, Dictionary<Guid, int> plantPositions)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            if (bookPositions.Any())
            {
                var bookIds = bookPositions.Keys.ToList();
                var bookShelves = await context.BookShelves
                    .Where(bs => bs.ShelfId == shelfId && bookIds.Contains(bs.BookId))
                    .ToListAsync();

                foreach (var bs in bookShelves)
                {
                    if (bookPositions.TryGetValue(bs.BookId, out var newPos))
                    {
                        bs.Position = newPos;
                    }
                }
            }

            if (plantPositions.Any())
            {
                var plantIds = plantPositions.Keys.ToList();
                var plantShelves = await context.PlantShelves
                    .Where(ps => ps.ShelfId == shelfId && plantIds.Contains(ps.PlantId))
                    .ToListAsync();

                foreach (var ps in plantShelves)
                {
                    if (plantPositions.TryGetValue(ps.PlantId, out var newPos))
                    {
                        ps.Position = newPos;
                    }
                }
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task MoveBookBetweenShelvesAsync(
        Guid sourceShelfId, Guid targetShelfId, Guid bookId, int targetPosition)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // 1. Remove from source shelf
            var sourceEntry = await context.BookShelves
                .FirstOrDefaultAsync(bs => bs.ShelfId == sourceShelfId && bs.BookId == bookId);
            if (sourceEntry != null)
                context.BookShelves.Remove(sourceEntry);

            // 2. Check if already on target shelf
            var existsOnTarget = await context.BookShelves
                .AnyAsync(bs => bs.ShelfId == targetShelfId && bs.BookId == bookId);

            if (!existsOnTarget)
            {
                int insertPos = targetPosition;
                if (insertPos < 0)
                {
                    // Append: find max position across books and plants
                    var maxPos = Math.Max(
                        await context.BookShelves.Where(bs => bs.ShelfId == targetShelfId)
                            .MaxAsync(bs => (int?)bs.Position) ?? -1,
                        await context.PlantShelves.Where(ps => ps.ShelfId == targetShelfId)
                            .MaxAsync(ps => (int?)ps.Position) ?? -1);
                    insertPos = maxPos + 1;
                }
                else
                {
                    // Shift items at targetPosition and beyond
                    var booksToShift = await context.BookShelves
                        .Where(bs => bs.ShelfId == targetShelfId && bs.Position >= insertPos)
                        .ToListAsync();
                    foreach (var b in booksToShift) b.Position++;

                    var plantsToShift = await context.PlantShelves
                        .Where(ps => ps.ShelfId == targetShelfId && ps.Position >= insertPos)
                        .ToListAsync();
                    foreach (var p in plantsToShift) p.Position++;
                }

                context.BookShelves.Add(new BookShelf
                {
                    ShelfId = targetShelfId,
                    BookId = bookId,
                    Position = insertPos
                });
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task MovePlantBetweenShelvesAsync(
        Guid sourceShelfId, Guid targetShelfId, Guid plantId, int targetPosition)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            // 1. Remove from source shelf
            var sourceEntry = await context.PlantShelves
                .FirstOrDefaultAsync(ps => ps.ShelfId == sourceShelfId && ps.PlantId == plantId);
            if (sourceEntry != null)
                context.PlantShelves.Remove(sourceEntry);

            // 2. Check if already on target shelf
            var existsOnTarget = await context.PlantShelves
                .AnyAsync(ps => ps.ShelfId == targetShelfId && ps.PlantId == plantId);

            if (!existsOnTarget)
            {
                int insertPos = targetPosition;
                if (insertPos < 0)
                {
                    var maxPos = Math.Max(
                        await context.BookShelves.Where(bs => bs.ShelfId == targetShelfId)
                            .MaxAsync(bs => (int?)bs.Position) ?? -1,
                        await context.PlantShelves.Where(ps => ps.ShelfId == targetShelfId)
                            .MaxAsync(ps => (int?)ps.Position) ?? -1);
                    insertPos = maxPos + 1;
                }
                else
                {
                    var booksToShift = await context.BookShelves
                        .Where(bs => bs.ShelfId == targetShelfId && bs.Position >= insertPos)
                        .ToListAsync();
                    foreach (var b in booksToShift) b.Position++;

                    var plantsToShift = await context.PlantShelves
                        .Where(ps => ps.ShelfId == targetShelfId && ps.Position >= insertPos)
                        .ToListAsync();
                    foreach (var p in plantsToShift) p.Position++;
                }

                context.PlantShelves.Add(new PlantShelf
                {
                    ShelfId = targetShelfId,
                    PlantId = plantId,
                    Position = insertPos
                });
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
